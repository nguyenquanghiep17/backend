# Architecture - Mail Library Abstraction

## Overview

This project uses **Repository Pattern** and **Dependency Inversion Principle** to abstract mail library operations. This allows swapping mail libraries (e.g., from MailKit to another library) without affecting business logic.

## Design Patterns Used

### 1. **Repository Pattern**
- `IMailAccountRepository`: Abstracts database access for mail accounts
- `MailAccountRepository`: Concrete implementation for MySQL

### 2. **Strategy Pattern / Adapter Pattern**
- `IMailClient`: Abstraction for mail client operations
- `IMailFolder`: Abstraction for mail folder operations
- `IMailClientFactory`: Factory for creating mail client instances
- `MailKitMailClient`: MailKit implementation (adapter)
- `MailKitMailFolder`: MailKit folder implementation (adapter)

### 3. **Dependency Inversion Principle**
- High-level modules (`MailService`, `MailMonitorService`) depend on abstractions (`IMailClient`, `IMailFolder`)
- Low-level modules (`MailKitMailClient`, `MailKitMailFolder`) implement these abstractions
- Dependencies are injected via constructor injection

## Architecture Layers

```
┌─────────────────────────────────────────┐
│   Business Logic Layer                  │
│   - MailService                         │
│   - MailMonitorService                  │
│   (No dependency on specific library)   │
└──────────────┬──────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────┐
│   Abstraction Layer                     │
│   - IMailClient                         │
│   - IMailFolder                         │
│   - IMailClientFactory                  │
└──────────────┬──────────────────────────┘
               │ implemented by
               ▼
┌─────────────────────────────────────────┐
│   Implementation Layer                  │
│   - MailKitMailClient                   │
│   - MailKitMailFolder                   │
│   - MailKitMailClientFactory            │
│   (Can be swapped with other libraries) │
└─────────────────────────────────────────┘
```

## How to Swap Mail Library

### Step 1: Create New Implementation

Create a new implementation folder, e.g., `Implementations/AnotherLibrary/`:

```csharp
// AnotherMailClient.cs
public class AnotherMailClient : IMailClient
{
    // Implement all IMailClient methods
    public Task ConnectAsync(...) { ... }
    public Task AuthenticateAsync(...) { ... }
    // ... etc
}

// AnotherMailClientFactory.cs
public class AnotherMailClientFactory : IMailClientFactory
{
    public IMailClient CreateClient()
    {
        return new AnotherMailClient();
    }
}
```

### Step 2: Update Dependency Injection

In `Program.cs`, change the factory registration:

```csharp
// Before (MailKit)
builder.Services.AddSingleton<IMailClientFactory, MailKitMailClientFactory>();

// After (New library)
builder.Services.AddSingleton<IMailClientFactory, AnotherMailClientFactory>();
```

That's it! No changes needed in `MailService` or `MailMonitorService`.

## Benefits

1. **Loose Coupling**: Business logic doesn't depend on specific mail library
2. **Easy Testing**: Can create mock implementations for unit testing
3. **Flexibility**: Swap libraries without changing business code
4. **Maintainability**: Changes to library implementation are isolated
5. **Open/Closed Principle**: Open for extension (new implementations), closed for modification (business logic)

## Current Implementation

- **Mail Library**: MailKit (via `MailKitMailClient`)
- **Database**: MySQL (via `MailAccountRepository`)
- **Allocation**: Hash-based distributed allocation for K8s scaling

## Future Extensions

To add support for another mail library:

1. Create new folder: `Implementations/NewLibrary/`
2. Implement `IMailClient`, `IMailFolder`, and `IMailClientFactory`
3. Register factory in `Program.cs`
4. No other code changes required!

