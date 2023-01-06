
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

public class ControllerBasedJsonInputFormatterMvcOptionsSetup : IConfigureOptions<MvcOptions>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MvcNewtonsoftJsonOptions _jsonOptions;
    private readonly ArrayPool<char> _charPool;
    private readonly ObjectPoolProvider _objectPoolProvider;
    public ControllerBasedJsonInputFormatterMvcOptionsSetup(
        ILoggerFactory loggerFactory,
        IOptions<MvcNewtonsoftJsonOptions> jsonOptions,
        ArrayPool<char> charPool,
        ObjectPoolProvider objectPoolProvider)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        if (jsonOptions == null)
        {
            throw new ArgumentNullException(nameof(jsonOptions));
        }

        if (charPool == null)
        {
            throw new ArgumentNullException(nameof(charPool));
        }

        if (objectPoolProvider == null)
        {
            throw new ArgumentNullException(nameof(objectPoolProvider));
        }

        _loggerFactory = loggerFactory;
        _jsonOptions = jsonOptions.Value;
        _charPool = charPool;
        _objectPoolProvider = objectPoolProvider;
    }
    public void Configure(MvcOptions options)
    {
        //remove the default
        options.InputFormatters.RemoveType<NewtonsoftJsonInputFormatter>();
        //add our own
        var jsonInputLogger = _loggerFactory.CreateLogger<ControllerBasedJsonInputFormatter>();

        options.InputFormatters.Add(new ControllerBasedJsonInputFormatter(
            jsonInputLogger,
            _jsonOptions.SerializerSettings,
            _charPool,
            _objectPoolProvider,
            options,
            _jsonOptions));
    }
}

public abstract class ContextAwareSerializerJsonInputFormatter : NewtonsoftJsonInputFormatter 
{        
    public ContextAwareSerializerJsonInputFormatter(ILogger logger, 
        JsonSerializerSettings serializerSettings, 
        ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider, MvcOptions options, MvcNewtonsoftJsonOptions jsonOptions) : base(logger, serializerSettings, charPool, objectPoolProvider, options, jsonOptions)
    {
        PoolProvider = objectPoolProvider;
    }
    readonly AsyncLocal<InputFormatterContext> _currentContextAsyncLocal = new AsyncLocal<InputFormatterContext>();
    readonly AsyncLocal<ActionContext> _currentActionAsyncLocal = new AsyncLocal<ActionContext>();
    protected InputFormatterContext CurrentContext => _currentContextAsyncLocal.Value;
    protected ActionContext CurrentAction => _currentActionAsyncLocal.Value;
    protected ObjectPoolProvider PoolProvider { get; }
    public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        _currentContextAsyncLocal.Value = context;
        _currentActionAsyncLocal.Value = context.HttpContext.RequestServices.GetRequiredService<IActionContextAccessor>().ActionContext;
        return base.ReadRequestBodyAsync(context, encoding); 
    }
    public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        _currentContextAsyncLocal.Value = context;
        _currentActionAsyncLocal.Value = context.HttpContext.RequestServices.GetRequiredService<IActionContextAccessor>().ActionContext;
        return base.ReadRequestBodyAsync(context);
    }
    protected virtual JsonSerializer CreateJsonSerializer(InputFormatterContext context) => null;
    protected override JsonSerializer CreateJsonSerializer()
    {
        var context = CurrentContext;
        return (context == null ? null : CreateJsonSerializer(context)) ?? base.CreateJsonSerializer();
    }
}

public abstract class ContextAwareMultiPooledSerializerJsonInputFormatter : ContextAwareSerializerJsonInputFormatter
{
    public ContextAwareMultiPooledSerializerJsonInputFormatter(ILogger logger, JsonSerializerSettings serializerSettings, ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider, MvcOptions options, MvcNewtonsoftJsonOptions jsonOptions) 
        : base(logger, serializerSettings, charPool, objectPoolProvider, options, jsonOptions)
    {
        
    }
    readonly IDictionary<object, ObjectPool<JsonSerializer>> _serializerPools = new ConcurrentDictionary<object, ObjectPool<JsonSerializer>>();
    readonly AsyncLocal<object> _currentPoolKeyAsyncLocal = new AsyncLocal<object>();
    protected object CurrentPoolKey => _currentPoolKeyAsyncLocal.Value;
    protected abstract object GetSerializerPoolKey(InputFormatterContext context);
    protected override JsonSerializer CreateJsonSerializer(InputFormatterContext context)
    {
        object poolKey = GetSerializerPoolKey(context) ?? "";
        if(!_serializerPools.TryGetValue(poolKey, out var pool))
        {
            //clone the settings
            var serializerSettings = new JsonSerializerSettings();
            foreach(var prop in typeof(JsonSerializerSettings).GetProperties().Where(e => e.CanWrite))
            {
                prop.SetValue(serializerSettings, prop.GetValue(SerializerSettings));
            }
            ConfigureSerializerSettings(serializerSettings, poolKey, context);
            pool = PoolProvider.Create(new JsonSerializerPooledPolicy(serializerSettings));
            _serializerPools[poolKey] = pool;
        }
        _currentPoolKeyAsyncLocal.Value = poolKey;
        return pool.Get();
    }
    protected override void ReleaseJsonSerializer(JsonSerializer serializer)
    {            
        if(_serializerPools.TryGetValue(CurrentPoolKey ?? "", out var pool))
        {
            pool.Return(serializer);
        }         
    }
    protected virtual void ConfigureSerializerSettings(JsonSerializerSettings serializerSettings, object poolKey, InputFormatterContext context) { }
}

//there is a similar class like this implemented by the framework 
//but it's a pity that it's internal
//So we define our own class here (which is exactly the same from the source code)
//It's quite simple like this
public class JsonSerializerPooledPolicy : IPooledObjectPolicy<JsonSerializer>
{
    private readonly JsonSerializerSettings _serializerSettings;
    
    public JsonSerializerPooledPolicy(JsonSerializerSettings serializerSettings)
    {
        _serializerSettings = serializerSettings;
    }

    public JsonSerializer Create() => JsonSerializer.Create(_serializerSettings);
    
    public bool Return(JsonSerializer serializer) => true;
}

public class ControllerBasedJsonInputFormatter : ContextAwareMultiPooledSerializerJsonInputFormatter,
    IControllerBasedJsonSerializerSettingsBuilder
{
    public ControllerBasedJsonInputFormatter(ILogger logger, JsonSerializerSettings serializerSettings, ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider, MvcOptions options, MvcNewtonsoftJsonOptions jsonOptions) : base(logger, serializerSettings, charPool, objectPoolProvider, options, jsonOptions)
    {
    }
    readonly IDictionary<object, Action<JsonSerializerSettings>> _configureSerializerSettings
             = new Dictionary<object, Action<JsonSerializerSettings>>();
    readonly HashSet<object> _beingAppliedConfigurationKeys = new HashSet<object>();
    protected override object GetSerializerPoolKey(InputFormatterContext context)
    {
        var routeValues = context.HttpContext.GetRouteData()?.Values;
        var controllerName = routeValues == null ? null : routeValues["controller"]?.ToString();
        if(controllerName != null && _configureSerializerSettings.ContainsKey(controllerName))
        {
            return controllerName;
        }
        var actionContext = CurrentAction;
        if (actionContext != null && actionContext.ActionDescriptor is ControllerActionDescriptor actionDesc)
        {
            foreach (var attr in actionDesc.MethodInfo.GetCustomAttributes(true)
                                           .Concat(actionDesc.ControllerTypeInfo.GetCustomAttributes(true)))
            {
                var key = attr.GetType();
                if (_configureSerializerSettings.ContainsKey(key))
                {                        
                    return key;
                }
            }
        }
        return null;
    }
    public IControllerBasedJsonSerializerSettingsBuilder ForControllers(params string[] controllerNames)
    {
        foreach(var controllerName in controllerNames ?? Enumerable.Empty<string>())
        {                
            _beingAppliedConfigurationKeys.Add((controllerName ?? "").ToLowerInvariant());
        }            
        return this;
    }
    public IControllerBasedJsonSerializerSettingsBuilder ForControllersWithAttribute<T>()
    {
        _beingAppliedConfigurationKeys.Add(typeof(T));
        return this;
    }
    public IControllerBasedJsonSerializerSettingsBuilder ForActionsWithAttribute<T>()
    {
        _beingAppliedConfigurationKeys.Add(typeof(T));
        return this;
    }
    ControllerBasedJsonInputFormatter IControllerBasedJsonSerializerSettingsBuilder.WithSerializerSettingsConfigurer(Action<JsonSerializerSettings> configurer)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        foreach(var key in _beingAppliedConfigurationKeys)
        {
            _configureSerializerSettings[key] = configurer;
        }
        _beingAppliedConfigurationKeys.Clear();
        return this;
    }
    protected override void ConfigureSerializerSettings(JsonSerializerSettings serializerSettings, object poolKey, InputFormatterContext context)
    {            
        if (_configureSerializerSettings.TryGetValue(poolKey, out var configurer))
        {
            configurer.Invoke(serializerSettings);
        }
    }
}
public interface IControllerBasedJsonSerializerSettingsBuilder
{
    ControllerBasedJsonInputFormatter WithSerializerSettingsConfigurer(Action<JsonSerializerSettings> configurer);
    IControllerBasedJsonSerializerSettingsBuilder ForControllers(params string[] controllerNames);
    IControllerBasedJsonSerializerSettingsBuilder ForControllersWithAttribute<T>();
    IControllerBasedJsonSerializerSettingsBuilder ForActionsWithAttribute<T>();
}
public static class ControllerBasedJsonInputFormatterServiceCollectionExtensions
{
    public static IServiceCollection AddControllerBasedJsonInputFormatter(this IServiceCollection services,
        Action<ControllerBasedJsonInputFormatter> configureFormatter)
    {
        if(configureFormatter == null)
        {
            throw new ArgumentNullException(nameof(configureFormatter));
        }
        services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();
        return services.ConfigureOptions<ControllerBasedJsonInputFormatterMvcOptionsSetup>()
                       .PostConfigure<MvcOptions>(o => {
                           var jsonInputFormatter = o.InputFormatters.OfType<ControllerBasedJsonInputFormatter>().FirstOrDefault();
                           if(jsonInputFormatter != null)
                           {
                               configureFormatter(jsonInputFormatter);
                           }
                       });
    }
}

//This attribute is used as a marker to decorate any controllers 
//or actions that you want to apply your custom input formatter
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class UseWasabiJsonInputFormatterAttribute : Attribute
{
}
