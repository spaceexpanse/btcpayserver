using System.Collections.Generic;
using System.Linq;
using BTCPayServer.Controllers.NewApi;
using BTCPayServer.Payments;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Xunit;

namespace BTCPayServer.Tests.NewApi
{
    public class PaymentMethodsControllerTests
    {
        [Fact]
        public void GetPaymentMethods()
        {
            var mocker = new AutoMocker();
            var controller = mocker.CreateInstance<PaymentMethodsController>();
            var result = controller.GetPaymentMethods();

            var paymentMethods =
                Assert.IsAssignableFrom<IEnumerable<string>>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(nameof(PaymentTypes.BTCLike), paymentMethods.First());
        }
    }
}
