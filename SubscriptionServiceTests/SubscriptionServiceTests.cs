using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using System;
using System.Collections.Generic;
using Xunit;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repoMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notifyMock;
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _repoMock = new Mock<IMemberRepository>();
            _paymentMock = new Mock<IPaymentService>();
            _notifyMock = new Mock<INotificationService>();
            _service = new SubscriptionService(_repoMock.Object, _paymentMock.Object, _notifyMock.Object);
        }

        ///<summary>
        /// Успішне продовження підписки: платіж підтверджено, дані оновлено
        /// Використовується Assert.True, Verify(Times.Exactly)
        ///</summary>
        [Fact]
        public void RenewSubscription_ValidPayment_ReturnsTrueAndUpdatesMember()
        {
            // Arrange
            var member = new Member { Id = 1, Name = "Anna", IsActive = false, SubscriptionEnd = DateTime.Now.AddDays(-10) };
            _repoMock.Setup(r => r.GetById(1)).Returns(member);
            _paymentMock.Setup(p => p.VerifyPayment(1, 499.99m)).Returns(true);

            // Act
            var result = _service.RenewSubscription(1, 499.99m, 30);

            // Assert
            Assert.True(result);
            Assert.True(member.IsActive);
            Assert.True(member.SubscriptionEnd >= DateTime.Now.AddDays(29));
            _repoMock.Verify(r => r.Update(member), Times.Once());
            _notifyMock.Verify(n => n.SendNotification("Subscription renewed!", 1), Times.Once());
        }

        ///<summary>
        /// Платіж не пройшов → підписка не продовжується
        /// Assert.False + Verify(..., Times.Never)
        ///</summary>
        [Fact]
        public void RenewSubscription_InvalidPayment_ReturnsFalseAndNoChanges()
        {
            // Arrange
            var member = new Member { Id = 1, IsActive = true };
            _repoMock.Setup(r => r.GetById(1)).Returns(member);
            _paymentMock.Setup(p => p.VerifyPayment(1, 100m)).Returns(false);

            // Act
            var result = _service.RenewSubscription(1, 100m, 30);

            // Assert
            Assert.False(result);
            _repoMock.Verify(r => r.Update(It.IsAny<Member>()), Times.Never());
            _notifyMock.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never());
        }

        ///<summary>
        /// Користувач не знайдений → кидається ArgumentException
        /// Assert.Throws
        ///</summary>
        [Fact]
        public void RenewSubscription_MemberNotFound_ThrowsArgumentException()
        {
            // Arrange
            _repoMock.Setup(r => r.GetById(999)).Returns((Member?)null);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                _service.RenewSubscription(999, 500m, 30));
            Assert.Equal("Member not found", ex.Message);
        }

        ///<summary>
        /// Параметризований тест продовження на різну кількість днів
        /// [Theory] + [InlineData]
        ///</summary>
        [Theory]
        [InlineData(7)]
        [InlineData(30)]
        [InlineData(90)]
        public void RenewSubscription_DifferentDays_SetsCorrectEndDate(int days)
        {
            // Arrange
            var member = new Member { Id = 10, IsActive = false };
            _repoMock.Setup(r => r.GetById(10)).Returns(member);
            _paymentMock.Setup(p => p.VerifyPayment(10, It.IsAny<decimal>())).Returns(true);

            // Act
            _service.RenewSubscription(10, 300m, days);

            // Assert
            var expectedMin = DateTime.Now.AddDays(days - 0.0001);
            var expectedMax = DateTime.Now.AddDays(days + 1);
            Assert.True(member.SubscriptionEnd >= expectedMin && member.SubscriptionEnd < expectedMax);
        }

        ///<summary>
        /// Деактивація прострочених підписок: кілька членів
        /// Assert.Contains, Assert.NotEmpty, Verify(..., Times.Exactly)
        ///</summary>
        [Fact]
        public void DeactivateExpiredMembers_MultipleExpired_DeactivatesAndNotifies()
        {
            // Arrange
            var expired1 = new Member { Id = 1, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-5) };
            var expired2 = new Member { Id = 2, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1) };
            var active = new Member { Id = 3, IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(10) };

            _repoMock.Setup(r => r.GetAll()).Returns(new List<Member> { expired1, expired2, active });

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.False(expired1.IsActive);
            Assert.False(expired2.IsActive);
            Assert.True(active.IsActive);

            _repoMock.Verify(r => r.Update(expired1), Times.Once());
            _repoMock.Verify(r => r.Update(expired2), Times.Once());
            _repoMock.Verify(r => r.Update(active), Times.Never());

            _notifyMock.Verify(n => n.SendNotification("Membership expired", 1), Times.Once());
            _notifyMock.Verify(n => n.SendNotification("Membership expired", 2), Times.Once());
        }

        ///<summary>
        /// Немає прострочених підписок → нічого не оновлюється
        /// Assert.Empty (в контексті викликів)
        ///</summary>
        [Fact]
        public void DeactivateExpiredMembers_NoExpiredMembers_NoUpdates()
        {
            // Arrange
            var members = new List<Member>
            {
                new() { Id = 1, SubscriptionEnd = DateTime.Now.AddDays(5) },
                new() { Id = 2, SubscriptionEnd = null, IsActive = true }
            };
            _repoMock.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            _repoMock.Verify(r => r.Update(It.IsAny<Member>()), Times.Never());
            _notifyMock.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never());
        }

        ///<summary>
        /// Перевірка аргументу за допомогою It.Is<>
        ///</summary>
        [Fact]
        public void RenewSubscription_ValidCall_UpdatesWithCorrectMember()
        {
            // Arrange
            var member = new Member { Id = 5, Name = "Test" };
            _repoMock.Setup(r => r.GetById(5)).Returns(member);
            _paymentMock.Setup(p => p.VerifyPayment(5, 599m)).Returns(true);

            // Act
            _service.RenewSubscription(5, 599m, 60);

            // Assert
            _repoMock.Verify(r => r.Update(It.Is<Member>(m => m.Id == 5 && m.IsActive == true)), Times.Once());
        }

        ///<summary>
        /// Використання It.IsAny<> для гнучкої перевірки
        ///</summary>
        [Fact]
        public void RenewSubscription_AnyValidPayment_CallsNotifyWithCorrectText()
        {
            // Arrange
            var member = new Member { Id = 7 };
            _repoMock.Setup(r => r.GetById(7)).Returns(member);
            _paymentMock.Setup(p => p.VerifyPayment(It.IsAny<int>(), It.IsAny<decimal>())).Returns(true);

            // Act
            _service.RenewSubscription(7, 1000m, 365);

            // Assert
            _notifyMock.Verify(n => n.SendNotification(It.IsAny<string>(), 7), Times.Once());
            _notifyMock.Verify(n => n.SendNotification("Subscription renewed!", It.IsAny<int>()), Times.Once());
        }
    }
}