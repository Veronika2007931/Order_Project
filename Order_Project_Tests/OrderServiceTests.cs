using System;
using System.Collections.Generic;
using Moq;
using Xunit;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    /// <summary>
    /// Набір юніт-тестів для перевірки бізнес-логіки класу OrderService з використанням xUnit та Moq.
    /// </summary>
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        /// <summary>
        /// Конструктор тестового класу. Ініціалізує ізольовані мок-об'єкти та екземпляр тестуємого сервісу перед кожним тестом.
        /// </summary>
        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(
                _inventoryMock.Object,
                _paymentMock.Object,
                _notificationMock.Object
            );
        }

        /// <summary>
        /// [Theory] / [InlineData]: Перевіряє викидання ArgumentException при спробі створити замовлення з невалідною кількістю (нульовою або від'ємною).
        /// </summary>
        /// <param name="quantity">Некоректна кількість товару.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-25)]
        public void CreateOrder_InvalidQuantity_ThrowsArgumentException(int quantity)
        {
            string product = "Laptop";

            Action act = () => _service.CreateOrder(product, quantity);

            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// Assert.Throws: Перевіряє валідацію вхідних даних при порожньому імені товару (повинен кидати ArgumentException).
        /// </summary>
        [Fact]
        public void CreateOrder_EmptyProductName_ThrowsArgumentException()
        {
            string emptyProduct = "";
            int quantity = 2;

            Action act = () => _service.CreateOrder(emptyProduct, quantity);

            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// Assert.Throws & Assert.Equal: Перевіряє сценарій нестачі товару на складі, коли CheckStock повертає false.
        /// </summary>
        [Fact]
        public void CreateOrder_NotEnoughStock_ThrowsInvalidOperationException()
        {
            string product = "Laptop";
            int quantity = 5;
            _inventoryMock.Setup(i => i.CheckStock(product, quantity)).Returns(false);

            Action act = () => _service.CreateOrder(product, quantity);

            var exception = Assert.Throws<InvalidOperationException>(act);
            Assert.Equal("Not enough stock.", exception.Message);
        }

        /// <summary>
        /// Assert.Equal: Перевіряє точну відповідність встановлених полів продукту та кількості при успішному створенні замовлення.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidOrder_SetsCorrectProductAndQuantity()
        {
            _inventoryMock.Setup(i => i.CheckStock("Mouse", 2)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("Mouse", 2);

            Assert.Equal("Mouse", order.Product);
            Assert.Equal(2, order.Quantity);
        }

        /// <summary>
        /// Assert.NotEqual: Перевіряє унікальність ідентифікаторів Id для різних замовлень.
        /// </summary>
        [Fact]
        public void CreateOrder_MultipleOrders_GeneratesDifferentIds()
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order1 = _service.CreateOrder("Item1", 1);
            var order2 = _service.CreateOrder("Item2", 1);

            Assert.NotEqual(order1.Id, order2.Id);
        }

        /// <summary>
        /// Assert.True: Перевіряє успішне оновлення кількості для існуючого замовлення.
        /// </summary>
        [Fact]
        public void UpdateOrder_ExistingOrderAndValidQuantity_ReturnsTrue()
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Screen", 1);

            bool result = _service.UpdateOrder(order.Id, 3);

            Assert.True(result);
            Assert.Equal(3, order.Quantity);
        }

        /// <summary>
        /// Assert.False: Перевіряє сценарій невалідного UpdateOrder, коли замовлення з переданим Id не знайдено в системі.
        /// </summary>
        [Fact]
        public void UpdateOrder_NonExistingOrder_ReturnsFalse()
        {
            int nonExistingOrderId = 999;

            bool result = _service.UpdateOrder(nonExistingOrderId, 5);

            Assert.False(result);
        }

        /// <summary>
        /// [Theory] / [InlineData] & Assert.False: Перевіряє сценарій невалідного UpdateOrder при спробі встановити нульову або від'ємну кількість.
        /// </summary>
        /// <param name="invalidQuantity">Некоректне оновлене значення кількості.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void UpdateOrder_InvalidQuantity_ReturnsFalse(int invalidQuantity)
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Keyboard", 2);

            bool result = _service.UpdateOrder(order.Id, invalidQuantity);

            Assert.False(result);
            Assert.Equal(2, order.Quantity);
        }

        /// <summary>
        /// Assert.True: Перевіряє успішне видалення існуючого замовлення, видалення його зі списку та повернення залишків на склад.
        /// </summary>
        [Fact]
        public void RemoveOrder_ExistingOrder_ReturnsTrueAndRestocksInventory()
        {
            _inventoryMock.Setup(i => i.CheckStock("Headphones", 2)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Headphones", 2);

            bool result = _service.RemoveOrder(order.Id);

            Assert.True(result);
            Assert.DoesNotContain(order, _service.GetOrders());
            _inventoryMock.Verify(i => i.IncreaseStock("Headphones", 2), Times.Once);
        }

        /// <summary>
        /// Assert.False: Перевіряє невдале видалення, коли передано Id неіснуючого замовлення.
        /// </summary>
        [Fact]
        public void RemoveOrder_NonExistingOrder_ReturnsFalse()
        {
            int nonExistingId = 404;

            bool result = _service.RemoveOrder(nonExistingId);

            Assert.False(result);
        }

        /// <summary>
        /// Assert.NotNull: Перевіряє повернення проініціалізованого об'єкта замовлення при валідній обробці.
        /// </summary>
        [Fact]
        public void CreateOrder_SuccessfulCreation_ReturnsNotNullOrder()
        {
            _inventoryMock.Setup(i => i.CheckStock("Monitor", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("Monitor", 1);

            Assert.NotNull(order);
        }

        /// <summary>
        /// Assert.Contains: Перевіряє наявність створеного замовлення у внутрішньому списку системи.
        /// </summary>
        [Fact]
        public void GetOrders_AfterAddingOrder_ContainsCreatedOrder()
        {
            _inventoryMock.Setup(i => i.CheckStock("SSD", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("SSD", 1);

            Assert.Contains(order, _service.GetOrders());
        }

        /// <summary>
        /// Assert.Empty / Assert.NotEmpty: Перевіряє стан списку замовлень (порожній спочатку, непорожній після додавання).
        /// </summary>
        [Fact]
        public void GetOrders_InitiallyEmpty_NotEmptyAfterAdd()
        {
            Assert.Empty(_service.GetOrders());

            _inventoryMock.Setup(i => i.CheckStock("RAM", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("RAM", 1);

            Assert.NotEmpty(_service.GetOrders());
        }

        /// <summary>
        /// Verify(): Базова перевірка обов'язкового виклику методу CheckStock при створенні замовлення.
        /// </summary>
        [Fact]
        public void CreateOrder_WhenValid_CallsCheckStock()
        {
            _inventoryMock.Setup(i => i.CheckStock("GPU", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("GPU", 1);

            _inventoryMock.Verify(i => i.CheckStock("GPU", 1), Times.Once);
        }

        /// <summary>
        /// Verify(..., Times.Never): Переконується, що підтвердження ніколи не відправляється клієнту, якщо оплата була неуспішною.
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFailed_NeverSendsNotification()
        {
            _inventoryMock.Setup(i => i.CheckStock("Headset", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Action act = () => _service.CreateOrder("Headset", 1);

            Assert.Throws<InvalidOperationException>(act);
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
        }

        /// <summary>
        /// Verify(..., Times.Exactly(1)): Переконується, що при збої оплати товар повертається на склад рівно 1 раз.
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFailed_IncreasesStockExactlyOnce()
        {
            _inventoryMock.Setup(i => i.CheckStock("Cable", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Action act = () => _service.CreateOrder("Cable", 1);

            Assert.Throws<InvalidOperationException>(act);
            _inventoryMock.Verify(i => i.IncreaseStock("Cable", 1), Times.Exactly(1));
        }

        /// <summary>
        /// Verify(..., Times.AtLeastOnce): Переконується, що списання залишку ReduceStock викликалося щонайменше один раз.
        /// </summary>
        [Fact]
        public void CreateOrder_Successful_ReducesStockAtLeastOnce()
        {
            _inventoryMock.Setup(i => i.CheckStock("Mic", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("Mic", 1);

            _inventoryMock.Verify(i => i.ReduceStock("Mic", 1), Times.AtLeastOnce());
        }

        /// <summary>
        /// It.Is(predicate): Перевіряє, що сповіщення SendConfirmation передає замовлення із відповідною назвою продукту та статусом IsPaid = true.
        /// </summary>
        [Fact]
        public void CreateOrder_Successful_SendsConfirmationWithCorrectProduct()
        {

            _inventoryMock.Setup(i => i.CheckStock("Webcam", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("Webcam", 1);

            _notificationMock.Verify(n => n.SendConfirmation(It.Is<Order>(o => o.Product == "Webcam" && o.IsPaid)), Times.Once);
        }

        /// <summary>
        /// It.IsAny: Перевіряє, що шлюз оплати ProcessPayment приймає будь-який об'єкт замовлення типу Order.
        /// </summary>
        [Fact]
        public void CreateOrder_Successful_ProcessPaymentAcceptsAnyOrder()
        {

            _inventoryMock.Setup(i => i.CheckStock("Hub", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("Hub", 1);

            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Once);
        }
    }
}