# PS-ADR-6: Validation with FluentValidation

**Related:** NF-1, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-5](error-handling.md)

## Context

The rules of the input belong to the use cases, not to the web layer.

## Decision

- Validation uses FluentValidation in the Application. Every command or request type gets its own `AbstractValidator<T>` next to it.
- The first step of a service is the validation with `await validator.ValidateAndThrowAsync(command, ct)`. `AddApplication` registers every validator of the assembly.
- Request DTOs have no DataAnnotations, and `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is true, so all content rules are in validators.
- The user name of ADR-4 is checked in two rules, one for its length and one for its characters, so each has its own message. A regular expression ends with `\z` instead of `$`, because in .NET `$` also matches before a final line break.
- The lengths of the password and of the email address count Unicode code points like the frontend, as the `maxLength` of the contract measures them. ADR-4 decides this for the password.
- An email address has one `@` with other characters on both sides and no whitespace, which is the rule of the frontend too. Identity checks only the `@`, so it accepts every address that the validator accepts.
- The messages of the account rules are the same as the ones of the frontend, so a user sees one text for one rule.
