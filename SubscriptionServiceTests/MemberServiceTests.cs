using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;
using System;
using Xunit;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repoMock;
        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _repoMock = new Mock<IMemberRepository>();
            _service = new MemberService(_repoMock.Object);
        }

        ///<summary>
        /// Перевірка, що GetMember повертає коректного користувача за існуючим Id
        /// Використовується Assert.Equal
        ///</summary>
        [Fact]
        public void GetMember_ExistingId_ReturnsMember()
        {
            // Arrange
            var expected = new Member { Id = 1, Name = "Олег", IsActive = true };
            _repoMock.Setup(r => r.GetById(1)).Returns(expected);

            // Act
            var result = _service.GetMember(1);

            // Assert
            Assert.Equal(expected, result);
            Assert.Same(expected, result);
        }

        ///<summary>
        /// Перевірка, що GetMember повертає null для неіснуючого Id
        /// Використовується Assert.Null
        ///</summary>
        [Fact]
        public void GetMember_NonExistingId_ReturnsNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetById(999)).Returns((Member?)null);

            // Act
            var result = _service.GetMember(999);

            // Assert
            Assert.Null(result);
        }

        ///<summary>
        /// Перевірка активності користувача з різними станами
        /// Параметризований тест через [Theory] + [InlineData]
        ///</summary>
        [Theory]
        [InlineData(1, true, true)]
        [InlineData(2, false, false)]
        [InlineData(3, null, false)] // член не знайдений
        public void IsActive_VariousScenarios_ReturnsCorrectBool(int memberId, bool? isActive, bool expected)
        {
            // Arrange
            var member = isActive.HasValue ? new Member { Id = memberId, IsActive = isActive.Value } : null;
            _repoMock.Setup(r => r.GetById(memberId)).Returns(member);

            // Act
            var result = _service.IsActive(memberId);

            // Assert
            Assert.Equal(expected, result);
        }

        ///<summary>
        /// Перевірка, що IsActive повертає false коли член не знайдений
        /// Використовується Assert.False
        ///</summary>
        [Fact]
        public void IsActive_MemberNotFound_ReturnsFalse()
        {
            // Arrange
            _repoMock.Setup(r => r.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var result = _service.IsActive(100);

            // Assert
            Assert.False(result);
        }

        ///<summary>
        /// Перевірка, що метод репозиторію викликається при запиті користувача
        /// Використовується Verify()
        ///</summary>
        [Fact]
        public void GetMember_CallsRepositoryGetById()
        {
            // Arrange
            _repoMock.Setup(r => r.GetById(5)).Returns(new Member());

            // Act
            _service.GetMember(5);

            // Assert
            _repoMock.Verify(r => r.GetById(5), Times.Once());
        }
    }
}