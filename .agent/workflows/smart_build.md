---
description: Build the application, attempting to avoid unnecessary user interruptions suitable for C# / .NET projects.
---

1.  **Attempt Build**: Run the build command immediately.
    ```bash
    dotnet build
    ```

2.  **Analyze Result**:
    *   **Success**: If the build succeeds, proceed to verification or notification.
    *   **Failure (File Locked)**: If the error indicates the file is in use (e.g., `The process cannot access the file...` or `...because it is being used by another process`), **ONLY THEN** notify the user:
        > "The build failed because the application is running. Please close it so I can finish the update."
        *   After the user confirms, retry Step 1.
    *   **Failure (Compilation Error)**: If it's a code error, diagnose and fix it normally.

3.  **Run**: After a successful build, automatically run the application.
    ```bash
    dotnet run
    ```
