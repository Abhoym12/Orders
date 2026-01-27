using FluentValidation.TestHelper;
using OrderService.Manager.Commands;
using OrderService.Manager.Validators;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Tests;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator;

    public CreateOrderCommandValidatorTests()
    {
        _validator = new CreateOrderCommandValidator();
    }

    [Fact]
    public void Validate_EmptyUserId_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.Empty,
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Product A", 1, 10.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_EmptyItems_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest>());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void Validate_ItemWithEmptyProductId_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest> { new(Guid.Empty, "Product A", 1, 10.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Items[0].ProductId")
            .WithErrorMessage("ProductId is required.");
    }

    [Fact]
    public void Validate_ItemWithZeroQuantity_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Product A", 0, 10.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Items[0].Quantity")
            .WithErrorMessage("Quantity must be greater than zero.");
    }

    [Fact]
    public void Validate_ItemWithNegativeQuantity_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Product A", -1, 10.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Items[0].Quantity")
            .WithErrorMessage("Quantity must be greater than zero.");
    }

    [Fact]
    public void Validate_ItemWithNegativePrice_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Product A", 1, -1.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Items[0].Price")
            .WithErrorMessage("Price cannot be negative.");
    }

    [Fact]
    public void Validate_ItemWithZeroPrice_IsValid()
    {
        // Arrange (Zero price is valid - could be free item)
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Free Item", 1, 0.00m) });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Product A", 2, 10.00m),
                new(Guid.NewGuid(), "Product B", 1, 25.00m)
            });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MultipleItemsWithOneInvalid_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Product A", 2, 10.00m),  // Valid
                new(Guid.NewGuid(), "Product B", 0, 25.00m)   // Invalid - zero quantity
            });

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("Items[1].Quantity");
    }
}
