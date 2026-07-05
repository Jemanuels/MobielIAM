# Contributing

Thank you for contributing to this research prototype. This document describes the project's standards, code style, and workflow.

## Guidelines

- Keep code simple, readable and well-documented.
- Follow .editorconfig rules. Use 4 spaces for indentation in C# files.
- Use PascalCase for public types and members. Private fields use camelCase with a leading underscore (`_field`).
- Keep methods small and single-responsibility. Prefer composition over inheritance.

## Branching and Pull Requests

- Work in feature branches named `feature/<short-description>`.
- Open Pull Requests (PRs) against `master` with a clear title and description.
- Request at least one reviewer before merging.
- Use squash merges for small changes; keep commit history clean.

## Commit Messages

- Use imperative, present-tense style (e.g., "Add login page").
- Include a short description and, if needed, a longer body for context.

## Testing

- Add unit tests for business logic where practical.
- Keep UI tests optional for the MVP.

## Running the App

- Open the solution in Visual Studio 2022.
- Ensure .NET 9 SDK is installed.
- Restore NuGet packages.
- Set the MAUI project as startup and deploy to the desired platform.

## Code of Conduct

Be respectful and collaborative.

## Contact

Project owner: Jemanuels
