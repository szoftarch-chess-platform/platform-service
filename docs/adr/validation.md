# PS-ADR-6: Validation with FluentValidation

**Related:** NF-1, [PS-ADR-1](clean-architecture.md), [PS-ADR-5](error-handling.md)

## Context

The rules of the input belong to the use cases, not to the web layer.

## Decision

- Validation uses FluentValidation in the Application. Every command or request type gets its own `AbstractValidator<T>` next to it.
- The first step of a service is the validation with `await validator.ValidateAndThrowAsync(command, ct)`. `AddApplication` registers every validator of the assembly.
- Request DTOs have no DataAnnotations, and `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is true, so all content rules are in validators.
