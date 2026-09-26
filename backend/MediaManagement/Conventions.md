# Conventions

- Use [] for Collection initialization.
- Use file-scoped namespaces
- Use string interpolation
- Prefer static method if possible
- Add postfix Async for methods returning tasks. Exclude public methods in controllers.

## Ef Core
- DbContext is Not thread-safe. Don't share contexts between threads. Make sure to await all async calls before continuing to use the context instance.
An InvalidOperationException thrown by EF Core code can put the context into an unrecoverable state. Such exceptions indicate a program error and are not designed to be recovered from.