using BTCPayServer.Validation;
using Xunit;

namespace BTCPayServer.Tests
{
    public class StringRangeAttributeTests
    {
        [Theory]
        [InlineData(new []{"a", "b", "c"}, "b", true )]
        [InlineData(new []{"a", "b", "c"}, "c", true )]
        [InlineData(new []{"a", "b", "c"}, "d", false )]
        [InlineData(new []{"a", "b", "c"}, "ab", false )]
        [InlineData(new []{"a", "b", ""}, "", true )]
        public void StringRangeAttribute_CanValidateValueCorrectly(string[] values, string value, bool valid)
        {
            var attr = new StringRangeAttribute()
            {
                AllowableValues = values
            };
            
            Assert.Equal(valid, attr.IsValid(value));
        }
    }
}
