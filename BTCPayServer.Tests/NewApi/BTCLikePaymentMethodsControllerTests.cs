using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Controllers.NewApi;
using Microsoft.AspNetCore.Mvc;
using Moq.AutoMock;
using Xunit;

namespace BTCPayServer.Tests.NewApi
{
    public class BTCLikePaymentMethodsControllerTests
    {
        [Fact]
        public void GetBtcLikePaymentMethods()
        {
            var mocker = new AutoMocker();
            
            var mock = mocker.GetMock<BTCPayNetworkProvider>();
            var mockBtc = mocker.GetMock<BTCPayNetwork>();
            mockBtc.SetupGet(network => network.CryptoCode).Returns("BTC");
            mock.Setup(provider => provider.GetAll()).Returns(() => new List<BTCPayNetwork>()
            {
                mockBtc.Object
            });
            mocker.Use(mock.Object);
            var controller = mocker.CreateInstance<BTCLikePaymentMethodsController>();
            var result = controller.GetBtcLikePaymentMethods();

            var paymentMethods =
                Assert.IsAssignableFrom<IEnumerable<BtcLikePaymentMethod>>(Assert.IsType<OkObjectResult>(result.Result)
                    .Value).ToList();

            Assert.Single(paymentMethods);
            Assert.Equal("BTC", paymentMethods.First().CryptoCode);
        }
    }
}
