# Contributing to Hunspell.NET

Thank you for your interest in contributing to Hunspell.NET! This project is an AI-driven port of the original Hunspell library to modern .NET 10.

## Goals

1. **Accuracy**: Port Hunspell functionality as accurately as possible
2. **Modern**: Leverage .NET 10 and C# 13 features
3. **Performance**: Optimize for modern .NET runtime
4. **Maintainability**: Write clean, idiomatic C# code

## How to Contribute

### Reporting Issues

- Search existing issues before creating a new one
- Include sample code that reproduces the issue
- Specify your .NET version and OS

### Submitting Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Add or update tests as needed
5. Ensure all tests pass (`dotnet test`)
6. Ensure code builds without warnings (`dotnet build`)
7. Commit your changes (`git commit -m 'Add amazing feature'`)
8. Push to the branch (`git push origin feature/amazing-feature`)
9. Open a Pull Request

### Code Style

- Use modern C# 13 features where appropriate
- Follow .NET naming conventions
- Enable nullable reference types
- Use file-scoped namespaces
- Add XML documentation comments for public APIs
- Keep methods focused and testable

### Testing

- Add unit tests for new functionality
- Maintain or improve code coverage
- Test on multiple platforms when possible

### Performance

- Avoid unnecessary allocations in hot paths
- Use `Span<T>` for stack-based buffers when appropriate
- Profile before optimizing

## Development Setup

### Requirements

- .NET 10 SDK or later
- Your favorite IDE (Visual Studio 2022, Rider, VS Code)

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running Sample

```bash
dotnet run --project samples/Hunspell.Sample/Hunspell.Sample.csproj
```

## Questions?

Feel free to open an issue for questions or discussions about the project.

## License

By contributing, you agree that your contributions will be licensed under the same tri-license (MPL 1.1/GPL 2.0/LGPL 2.1) as the original Hunspell project.
