using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);
        }



        /// <summary>
        /// Перевіряє, що передача некоректної кількості товару (нульової або від'ємної) призводить до виклику винятку ArgumentException.
        /// </summary>

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void CreateOrder_InvalidQuantity_throwsArgumentExeption(int quantity)
        {
            Assert.Throws<ArgumentException>(() => _service.CreateOrder("Laptop", quantity));
        }

        /// <summary>
        /// Перевіряє точну відповідність встановлених полів створеного замовлення (Product, Quantity) очікуваним вхідним даним.
        /// </summary>

        [Fact]
        public void CreateOrder_ValidOrder_SetsCorrectProductAndQuabtity()
        {
            _inventoryMock.Setup(i => i.CheckStock("Mouse", 2)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("Mouse", 2);

            Assert.Equal("Mouse", order.Product);
            Assert.Equal(2, order.Quantity);
        }

        /// <summary>
        /// Перевіряє, що послідовне створення декількох замовлень генерує для них унікальні ідентифікатори Id.
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
        /// Перевіряє булеве значення: оновлення замовлення має повертати true у разі успішної зміни кількості.
        /// </summary>

        [Fact]
        public void UpdateOrder_ExistingOrder_ReturnsTrue()
        {
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(P => P.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Screen", 1);

            bool result = _service.UpdateOrder(order.Id, 3);
            Assert.True(result);

        }

        /// <summary>
        /// Перевіряє протилежне булеве значення: спроба видалення неіснуючого замовлення повинна повертати false.
        /// </summary>

        [Fact]
        public void RemoveOrder_NonExistingOrder_ReturnsFalse()
        {
            bool result = _service.RemoveOrder(999);
            Assert.False(result);
        }

        /// <summary>
        /// Перевіряє, що метод CreateOrder повертає ініціалізований об'єкт замовлення при успішній валідації та оплаті.
        /// </summary>

        [Fact]
        public void CreateOrder_SuccessfulCreation_ReturnsNotNullOrder()
        {
            _inventoryMock.Setup(i => i.CheckStock("Keyboard", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            var order = _service.CreateOrder("Keyboard", 1);
            Assert.NotNull(order);
        }

        /// <summary>
        /// Перевіряє, що внутрішня колекція замовлень містить щойно створений екземпляр замовлення.
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
        /// Перевіряє перехід стану колекції замовлень: початкову порожність та наявність елементів після створення замовлення.
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
        /// Перевіряє викидання винятку ArgumentException при спробі передати порожнє ім'я товару.
        /// </summary>

        [Fact]
        public void CreateOrder_EmptyProductName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.CreateOrder("", 2));
        }

        /// <summary>
        /// Перевіряє факт базового виклику методу CheckStock у залежності IInventoryService під час створення замовлення.
        /// </summary>

        [Fact]
        public void CreateOrder_WhenValid_CallsCheckStock()
        {
            _inventoryMock.Setup(i => i.CheckStock("GPU", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            _service.CreateOrder("GPU", 1);
            _inventoryMock.Verify(i => i.CheckStock("GPU", 1));

        }

        /// <summary>
        /// Переконується, що метод відправки сповіщення  ніколи не викликається у випадку невдалої оплати.
        /// </summary>

        [Fact]
        public void CreateOrder_PaymentFailed_NeverSendsNotification()
        {
            _inventoryMock.Setup(i => i.CheckStock("Headset", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder("Headset", 1));
            _notificationMock.Verify(n => n.SendConfirmation(It.IsAny<Order>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє точну кількість викликів: метод повернення товару на склад  має викликатися рівно 1 раз при збої оплати.
        /// </summary>

        [Fact]
        public void CreateOrder_PaymentFailed_IncreasesStockExactlyOnce()
        {
            _inventoryMock.Setup(i => i.CheckStock("Cable", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Assert.Throws<InvalidOperationException>(() => _service.CreateOrder("Cable", 1));

            _inventoryMock.Verify(i => i.IncreaseStock("Cable", 1), Times.Exactly(1));

        }

        /// <summary>
        /// Перевіряє гнучку умову кількості викликів: переконується, що метод списування зі складу  викликався хоча б один раз.
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
        /// Перевіряє аргумент виклику з предикатом: сповіщення  викликається саме з тим замовленням, що має відповідну назву товару та статус оплати.
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
        /// Перевіряє, що метод проведення оплати  викликається із будь-яким екземпляром типу Order.
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
