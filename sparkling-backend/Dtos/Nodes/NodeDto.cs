using FluentValidation;

namespace Sparkling.Backend.Dtos.Nodes;

public class NodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsLocal { get; set; } = false;
    public bool IsActive { get; set; } = false;
    public string SshPublicKey { get; set; } = string.Empty;
}

public class NodeDtoValidator : AbstractValidator<NodeDto>
{
    public NodeDtoValidator()
    {
        RuleFor(request => request.Name)
            .NotNull()
            .NotEmpty();

        RuleFor(request => request.Description)
            .NotNull();

        RuleFor(request => request.Address)
            .NotNull()
            .NotEmpty();
    }
}