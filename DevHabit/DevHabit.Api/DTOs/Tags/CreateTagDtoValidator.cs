using FluentValidation;

namespace DevHabit.Api.DTOs.Tags;

public sealed class CreateTagDtoValidator : AbstractValidator<CreateTagDto>
{
    public CreateTagDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MinimumLength(3);
        RuleFor(dto => dto.Description)
            .MaximumLength(50);
    }
}
