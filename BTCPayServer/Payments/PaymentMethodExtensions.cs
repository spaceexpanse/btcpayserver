﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Payments.Changelly;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Payments
{
    public class PaymentMethodExtensions
    {
        public static ISupportedPaymentMethod Deserialize(PaymentMethodId paymentMethodId, JToken value, BTCPayNetwork network)
        {
            //Todo: Abstract
            if (paymentMethodId.PaymentType == PaymentTypes.BTCLike)
            {
                if (value is JObject jobj)
                {
                    var scheme = network.NBXplorerNetwork.Serializer.ToObject<DerivationSchemeSettings>(jobj);
                    scheme.Network = network;
                    return scheme;
                }
                // Legacy
                else
                {
                    return BTCPayServer.DerivationSchemeSettings.Parse(((JValue)value).Value<string>(), network);
                }
            }
            //////////
            else if (paymentMethodId.PaymentType == PaymentTypes.LightningLike)
            {
                return JsonConvert.DeserializeObject<Payments.Lightning.LightningSupportedPaymentMethod>(value.ToString());
            }
            throw new NotSupportedException();
        }

        public static IPaymentMethodDetails DeserializePaymentMethodDetails(PaymentMethodId paymentMethodId, JObject jobj)
        {
            //Todo: Abstract
            if(paymentMethodId.PaymentType == PaymentTypes.BTCLike)
            {
                return JsonConvert.DeserializeObject<Payments.Bitcoin.BitcoinLikeOnChainPaymentMethod>(jobj.ToString());
            }
            if (paymentMethodId.PaymentType == PaymentTypes.LightningLike)
            {
                return JsonConvert.DeserializeObject<Payments.Lightning.LightningLikePaymentMethodDetails>(jobj.ToString());
            }
            throw new NotSupportedException(paymentMethodId.PaymentType.ToString());
        }


        public static JToken Serialize(ISupportedPaymentMethod factory)
        {
            // Legacy
            if (factory.PaymentId.PaymentType == PaymentTypes.BTCLike)
            {
                var derivation = (DerivationSchemeSettings)factory;
                var str = derivation.Network.NBXplorerNetwork.Serializer.ToString(derivation);
                return JObject.Parse(str);
            }
            //////////////
            else
            {
                var str = JsonConvert.SerializeObject(factory);
                return JObject.Parse(str);
            }
            throw new NotSupportedException();
        }

    }
}
