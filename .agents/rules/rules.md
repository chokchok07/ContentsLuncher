---
trigger: always_on
---

# AI Assistant Strict Guidelines: C# WinForms & Vanilla Web

## 1. Tech Stack & Environment (CRITICAL)
- **C# Desktop:** .NET Framework (v4.0.30319).
- **UI Framework:** Windows Forms (WinForms).
- **Build System:** Custom batch file using `csc.exe`. Do NOT use MSBuild or `dotnet build` commands.
- **Web Prototype:** Vanilla HTML, CSS, and pure JavaScript. NO heavy frontend frameworks (e.g., React, Vue) or bundlers.

## 2. Token & Context Management
- **NO FULL WORKSPACE SCAN:** Do not read the entire workspace. Only analyze the specific files provided in the user's prompt.
- **STOP & PROPOSE:** If a task requires modifying more than 2 files, STOP immediately. Propose a step-by-step plan before writing any code.
- **DIFF-ONLY OUTPUT:** NEVER output entire files. Provide only the modified blocks (methods/properties). Use `// ... existing code ...` for unchanged parts.

## 3. WinForms & C# Specific Rules
- **Dependency Control:** DO NOT suggest NuGet packages. Use ONLY the assemblies explicitly referenced via `csc.exe` (e.g., `System.Windows.Forms`, `System.Web.Extensions`).
- **UI Theming (Dark/Light Mode):** WinForms does not have XAML resource dictionaries. When changing themes, DO NOT hardcode colors in individual controls. Propose a static `ThemeManager` class or a `BaseForm` that iterates through child controls to apply colors programmatically.
- **Legacy Compatibility:** Ensure all C# code is compatible with C# 4.0/5.0 syntax. Do not use top-level statements or modern C# (9.0+) features like record types or pattern matching.
- **Single EXE Enforced:** The final output MUST be a standalone `.exe`. DO NOT use Newtonsoft.Json or any third-party NuGet packages that generate external `.dll` files. For JSON parsing, use ONLY the built-in `System.Web.Script.Serialization.JavaScriptSerializer` via `System.Web.Extensions`. Windows Native API calls (e.g., user32.dll) are allowed.

## 4. Vanilla Web Specific Rules
- **CSS Theming:** Use standard CSS custom properties (variables like `--bg-color`) in the `:root` selector for styling and theme toggling.
- **JavaScript:** Keep it raw and dependency-free. Do not use jQuery or external libraries unless specifically asked.

## 5. Anti-Yapping (Zero Filler)
- **NO CHITCHAT:** Do not apologize, do not say "Here is the code", and do not explain what you are going to do. 
- **Direct Output:** Start your response directly with the solution or the code block.