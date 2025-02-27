using FiniteAutomatons.Core.Models;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core
{
    public class ErrorViewModelTests
    {
        [Fact]
        public void ShowRequestId_ShouldReturnTrue_WhenRequestIdIsNotNullOrEmpty()
        {
            // Arrange
            var errorViewModel = new ErrorViewModel
            {
                RequestId = "123"
            };

            // Act
            var result = errorViewModel.ShowRequestId;

            // Assert
            result.ShouldBeTrue();
        }

        [Fact]
        public void ShowRequestId_ShouldReturnFalse_WhenRequestIdIsNull()
        {
            // Arrange
            var errorViewModel = new ErrorViewModel
            {
                RequestId = null
            };

            // Act
            var result = errorViewModel.ShowRequestId;

            // Assert
            result.ShouldBeFalse();
        }

        [Fact]
        public void ShowRequestId_ShouldReturnFalse_WhenRequestIdIsEmpty()
        {
            // Arrange
            var errorViewModel = new ErrorViewModel
            {
                RequestId = string.Empty
            };

            // Act
            var result = errorViewModel.ShowRequestId;

            // Assert
            result.ShouldBeFalse();
        }
    }
}

