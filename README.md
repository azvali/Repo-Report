# Repo Report

Repo Report is a web application that uses AI to generate summaries of a GitHub repository's recent commit history. Provide a repository URL and a number of commits, and the tool will return a high-level overview of the changes as well as a detailed summary for each individual commit.

This provides a quick and powerful way for developers to get up to speed on the recent progress and evolution of a project.


## Features

-   **Overall Summary**: Get a high-level, AI-generated summary of the recent changes in a repository.
-   **Commit-by-Commit Details**: View individual, AI-generated summaries for each commit.
-   **GitHub Integration**: Directly links to the relevant commits on GitHub.
-   **Modern UI**: A sleek, dark-themed, and responsive user interface.
-   **Secure**: Uses .NET's User Secrets to securely manage the OpenAI API key during development.

## Tech Stack

-   **Frontend**: React, Vite
-   **Backend**: .NET 9 Minimal API, C#
-   **AI**: OpenAI (via `gpt-4o-mini`)

## Getting Started

Follow these instructions to set up and run the project locally on your machine.

### Prerequisites

-   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
-   [Node.js](https://nodejs.org/) (which includes npm)
-   An [OpenAI API Key](https://platform.openai.com/account/api-keys)

### 1. Clone the Repository

Clone this repository to your local machine:

```bash
git clone https://github.com/your-username/Repo-Report.git
cd Repo-Report
```

### 2. Configure the Backend

The backend server requires an OpenAI API key to function. This is handled securely using .NET's User Secrets, so you won't risk committing your key to source control.

1.  **Navigate to the server project directory:**
    ```bash
    cd server/server
    ```

2.  **Initialize User Secrets:**
    ```bash
    dotnet user-secrets init
    ```

3.  **Set your OpenAI API key:** (Replace `your_api_key_here` with your actual key)
    ```bash
    dotnet user-secrets set "OPENAI_API_KEY" "your_api_key_here"
    ```

4.  **Run the backend server:**
    ```bash
    dotnet run
    ```
    The server will start on `http://localhost:5135`.

### 3. Configure the Frontend

1.  **Navigate to the client project directory in a new terminal:**
    ```bash
    cd client/repo-report
    ```

2.  **Install dependencies:**
    ```bash
    npm install
    ```

3.  **Run the frontend development server:**
    ```bash
    npm run dev
    ```
    The React application will open and be accessible at `http://localhost:5173` (or another port if 5173 is in use).

## How to Use

1.  Open the web application in your browser.
2.  Enter the number of recent commits you want to analyze in the first input box.
3.  Paste the full URL of a public GitHub repository into the second input box.
4.  Click the "Summarize" button.
5.  View the generated overall summary and the detailed commit-by-commit breakdown.