# 🚀 CareerIQ — AI-Powered Resume Analyzer

CareerIQ is a full-stack AI-powered resume analysis application built using **Angular**, **ASP.NET Core Web API**, and a configurable AI provider. Groq is the default analysis provider; local **Gemma 3** through **Ollama** remains available.

The application analyzes resume content and generates structured insights including:

- 🛠️ Technical skills
- 💪 Strengths
- ⚠️ Areas for improvement
- 💡 AI-generated recommendations

The project demonstrates integrating AI/LLM capabilities into a full-stack application, with the option to use either a hosted provider or local inference.

**Default setup:** Groq handles resume analysis and requires an API key. Ollama is optional for local inference or local RAG embeddings. RAG is disabled by default.

---

## ✨ Features

- 🤖 AI-powered resume analysis
- ⚡ Groq-hosted resume analysis by default
- 🧠 Optional local LLM inference using Gemma 3 and Ollama
- 🔌 ASP.NET Core REST API
- 🅰️ Angular frontend
- 🎨 Responsive CareerIQ UI
- 🎯 SCSS styling
- 📦 Structured JSON AI responses
- 💉 Dependency Injection
- 🔄 HttpClientFactory
- 🧩 AI service abstraction using `IAiService`
- 📋 Automatic skill extraction
- 💪 Strength identification
- ⚠️ Weakness detection
- 💡 AI-generated recommendations
- 📖 Swagger/OpenAPI documentation
- 🔐 CORS configuration
- ⏳ Loading and error states
- 📄 Resume file uploads for PDF, DOCX, legacy DOC, and CSV
- 🔎 Server-side file signature validation and text extraction (10 MB maximum)
- 🔐 Configurable local and hosted AI providers

---

## 🎬 How It Works

1. Select a PDF, DOCX, legacy DOC, or CSV resume, or paste resume text.
2. Angular validates the selected input and sends it to the ASP.NET Core API.
3. For file uploads, the API validates the file again and extracts its text in memory.
4. If enabled, local RAG retrieves relevant passages from the Markdown knowledge base using Ollama embeddings.
5. The selected AI provider analyzes the resume: Groq by default, or local Ollama/Gemma 3.
6. The API returns the existing structured response and Angular displays the actual analysis.

The uploaded file is not permanently stored. With the default Groq provider, extracted resume text is sent to Groq; with Ollama selected, analysis runs locally. Enabling RAG uses Ollama locally for embeddings but does not make Groq analysis local.

---

## 📸 Screenshots

### 📝 Resume Analyzer

The Angular frontend provides the responsive CareerIQ analyzer, with resume file upload and a paste-text fallback.

![Resume Analyzer](screenshots/resume-analyzer.png)

---

### 🤖 AI Resume Analysis

After the analysis is completed, CareerIQ displays detected skills, strengths, areas for improvement, and AI-generated recommendations.

![AI Analysis Results](screenshots/ai-analysis-results.png)

---

### 🧠 Local Gemma 3 Response (Optional Provider)

This screenshot shows the optional local Ollama/Gemma 3 provider. Groq is the configured default.

![Ollama Gemma Response](screenshots/ollama-response.png)

---

## 🏗️ Architecture

```text
Browser
  │
  ▼
Angular CareerIQ UI (localhost:4200)
  │
  ├── POST /api/Resume/analyze       JSON: { resumeText }
  └── POST /api/Resume/analyze-file  multipart/form-data: file
                                      │
                                      ▼
                         ASP.NET Core API (localhost:5155)
                                      │
                          ResumeController
                                      │
                   ┌──────────────────┴──────────────────┐
                   │ file upload?                        │
                   ▼                                     │
       Validate size/type/signature                     │
                   │                                     │
       Extract PDF/DOCX/DOC/CSV text                    │
                   └──────────────────┬──────────────────┘
                                      ▼
                                   IAiService
                                      │
                          Optional local RAG context
                                      │
                       ┌──────────────┴──────────────┐
                       ▼                             ▼
             GroqAiService (default)       OllamaAiService (optional)
                       │                             │
             Groq Chat Completions          Ollama → Gemma 3
                       └──────────────┬──────────────┘
                                      ▼
                         ResumeAnalysisResponse JSON
                                      │
                                      ▼
                     Angular results cards and skills
```

### Optional local RAG data flow

```text
Resume text ───────────────┐
                           ▼
Markdown guidance ──→ Ollama embedding model
                           │
                   cosine-similarity search
                           │
                   top matching passages
                           │
                           ▼
              Selected AI provider prompt
                           │
             ┌─────────────┴─────────────┐
             ▼                           ▼
        Groq API                    Local Ollama
    (resume is sent)                 (Gemma 3)
```

RAG is disabled by default. When enabled with Groq, Ollama performs local embeddings/retrieval, but Groq still receives the resume text and retrieved guidance for analysis.

---

## 🔄 End-to-End Request Flow

```text
Text input:
Angular → POST /api/Resume/analyze { resumeText } ─────────────┐
                                                               │
File input:                                                    │
Angular → POST /api/Resume/analyze-file (multipart file)       │
                 │                                             │
                 ▼                                             │
         validate and extract text                             │
                 └──────────────────────┬──────────────────────┘
                                        ▼
                              ResumeController
                                        │
                              optional local RAG
                                        │
                              selected IAiService
                                        │
                        Groq (default) or Ollama
                                        │
                          structured JSON response
                                        │
                                        ▼
                         Angular renders results
```

---

## 🛠️ Technology Stack

### Frontend

- Angular
- TypeScript
- SCSS
- RxJS
- Angular HttpClient
- Angular Signals

### Backend

- ASP.NET Core Web API
- C#
- .NET
- REST APIs
- Dependency Injection
- HttpClientFactory
- DTOs
- Swagger / OpenAPI
- CORS
- PdfPig (Apache-2.0) for PDF text extraction
- Open XML SDK (MIT) for DOCX text extraction
- NPOI.HWPFCore (Apache-2.0) for legacy DOC text extraction
- .NET `TextFieldParser` for quoted CSV parsing

### AI

- Groq Chat Completions API (default; `openai/gpt-oss-20b`)
- Optional Ollama local inference (`gemma3`)
- Optional Ollama embeddings (`nomic-embed-text`) for local RAG
- Local Markdown knowledge base and in-memory cosine-similarity search
- Prompt Engineering
- Structured JSON Schema response

### Development Tools

- Visual Studio Code
- Git
- GitHub
- Swagger UI
- Groq (default AI provider)
- Optional Ollama for local inference/RAG
- Node.js
- .NET SDK

---

## 📁 Project Structure

```text
ResumeIQ-AI/
│
├── ResumeIQ.API/
│   │
│   ├── Controllers/
│   │   └── ResumeController.cs
│   │
│   ├── Models/
│   │   ├── ResumeAnalysisRequest.cs
│   │   └── ResumeAnalysisResponse.cs
│   │
│   ├── Services/
│   │   ├── IAiService.cs
│   │   ├── GroqAiService.cs
│   │   ├── OllamaAiService.cs
│   │   ├── IRagContextService.cs
│   │   ├── OllamaRagContextService.cs
│   │   ├── ResumeAnalysisPrompt.cs
│   │   ├── ResumeAnalysisResponseParser.cs
│   │   └── ResumeDocumentExtractor.cs
│   │
│   ├── KnowledgeBase/
│   │   └── resume-guidance.md
│   │
│   ├── .env.example
│   ├── Properties/
│   │   └── launchSettings.json
│   │
│   ├── Program.cs
│   ├── appsettings.json
│   └── ResumeIQ.API.csproj
│
├── ResumeIQ.API.Tests/
│   ├── GroqAiServiceTests.cs
│   ├── OllamaRagContextServiceTests.cs
│   ├── ResumeDocumentExtractorTests.cs
│   └── ResumeIQ.API.Tests.csproj
│
├── resume-iq-ui/
│   │
│   ├── src/
│   │   └── app/
│   │       │
│   │       ├── app.config.ts
│   │       ├── core/
│   │       │   ├── models/
│   │       │   │   ├── resume-analysis.model.ts
│   │       │   │   └── resume-analysis-request.model.ts
│   │       │   │
│   │       │   └── services/
│   │       │       ├── resume.ts
│   │       │       └── resume.spec.ts
│   │       │
│   │       └── features/
│   │           └── resume-analyzer/
│   │               ├── resume-analyzer.ts
│   │               ├── resume-analyzer.html
│   │               ├── resume-analyzer.scss
│   │               └── resume-analyzer.spec.ts
│   │
│   ├── angular.json
│   ├── package.json
│   └── tsconfig.json
│
├── screenshots/
│   ├── resume-analyzer.png
│   ├── ai-analysis-results.png
│   └── ollama-response.png
│
├── .gitignore
└── Readme.md
```

---

## 🤖 AI Integration

CareerIQ supports two analysis providers. **Groq is the default** and runs the `openai/gpt-oss-20b` model through Groq's hosted API. **Ollama is optional** for local inference with Gemma 3. Choose the provider using `Ai:Provider` in `ResumeIQ.API/appsettings.json`.

```text
ASP.NET Core
  │
  ├── Ai:Provider = Groq (default)
  │      └── Groq Chat Completions API → JSON analysis
  │
  └── Ai:Provider = Ollama
         └── Ollama API → Gemma 3 → JSON analysis
```

For Groq, the backend sends extracted resume text to Groq for analysis. For Ollama, inference runs locally. Uploaded resume files are not permanently stored by the API. Review the selected provider and privacy implications before submitting a resume.

---

## 🧠 AI Analysis

CareerIQ currently analyzes four major areas.

The model returns skills, strengths, weaknesses, and recommendations. CareerIQ does not invent a numeric resume score or ATS score.

### 🛠️ Skills

The AI identifies technical and professional skills from the resume.

Example:

```text
C#
ASP.NET Core
Angular
SQL Server
REST APIs
Entity Framework Core
Azure
```

### 💪 Strengths

The AI identifies areas where the resume demonstrates strong experience.

Example:

```text
Experience with REST APIs
Full-stack development
Backend development
Database management
Cloud exposure
```

### ⚠️ Weaknesses

The AI identifies missing information or areas that could be improved.

Example:

```text
Limited project metrics
Missing quantifiable achievements
Insufficient project details
```

### 💡 Recommendations

The AI generates suggestions to improve the resume.

Example:

```text
Add measurable achievements
Provide project details
Describe technical contributions
Highlight project impact
```

---

## 🔌 API

Both endpoints return the same response contract:

```json
{
  "skills": ["Angular"],
  "strengths": ["Experience supported by the resume"],
  "weaknesses": [],
  "recommendations": ["Add measurable outcomes when you can support them"]
}
```

The values above illustrate the JSON shape only; actual results depend on the submitted resume and AI response. No score or ATS result is fabricated.

### Analyze Resume

```http
POST /api/Resume/analyze
```

### Request

```json
{
  "resumeText": "I have experience developing REST APIs using ASP.NET Core, Angular and SQL Server."
}
```

### Response

```json
{
  "skills": [
    "ASP.NET Core",
    "Angular",
    "SQL Server",
    "REST APIs"
  ],
  "strengths": [
    "Backend development",
    "Full-stack development"
  ],
  "weaknesses": [
    "Limited information about project achievements"
  ],
  "recommendations": [
    "Add measurable achievements to projects",
    "Provide more information about project complexity"
  ]
}
```

### Analyze Resume File

```http
POST /api/Resume/analyze-file
Content-Type: multipart/form-data
```

Send the resume in the `file` form field. PDF, DOCX, legacy DOC, and CSV files are supported up to 10 MB. The backend validates the extension and file signature, extracts text in memory, and sends that text through the existing `IAiService`. Files are not permanently stored. Image-only PDFs do not produce extractable text; OCR is not enabled.

The endpoint returns the same `ResumeAnalysisResponse` JSON structure as the text endpoint. Extraction failures return a useful `message` and an appropriate 4xx status.

PDF extraction uses PdfPig (Apache-2.0), DOCX extraction uses the Open XML SDK (MIT), legacy DOC extraction uses NPOI.HWPFCore and its Apache-2.0 NPOI dependency, and CSV uses .NET's built-in `TextFieldParser`. Legacy DOC parsing is read-only and does not require Microsoft Office. No additional commercial document license is required.

---

## 🧩 Backend Design

The AI functionality is abstracted behind an `IAiService` interface.

```text
ResumeController
       │
       ▼
   IAiService
       ├── GroqAiService (default)
       └── OllamaAiService (optional)
```

The controller does not directly depend on a provider implementation. Provider selection happens in `Program.cs` through `Ai:Provider`.

Instead, it depends on the abstraction:

```csharp
IAiService
```

This provides loose coupling and makes the AI implementation replaceable.

For example, future implementations could be:

```text
IAiService
   │
   ├── GroqAiService
   └── OllamaAiService
```

---

## 💉 Dependency Injection

The application uses ASP.NET Core's built-in Dependency Injection system.

The AI service is registered through the application startup configuration.

The controller receives the abstraction rather than creating the service manually.

This follows the Dependency Inversion Principle and makes the application easier to test and maintain.

---

## 🌐 HttpClientFactory

The backend uses `HttpClientFactory` for Groq chat completions and Ollama inference/embeddings.

Instead of manually creating `HttpClient` instances, the application uses ASP.NET Core's `HttpClientFactory` infrastructure.

This provides:

- Centralized HTTP client configuration
- Dependency Injection
- Better connection management
- Testability
- Cleaner service implementation

---

## 📦 Strongly Typed AI Response

The AI response is converted into a strongly typed C# response model.

The structure contains:

```text
ResumeAnalysisResponse
│
├── Skills
├── Strengths
├── Weaknesses
└── Recommendations
```

This provides a defined contract between:

```text
AI
 ↓
ASP.NET Core
 ↓
Angular
```

The Angular application also uses a TypeScript interface representing the same response structure.

---

## 🧠 Prompt Engineering

The AI service sends a structured prompt to the configured provider and requests skills, strengths, weaknesses, and recommendations grounded in the resume.

The prompt instructs the model to analyze:

1. Skills
2. Strengths
3. Weaknesses
4. Recommendations

The model is also instructed to return structured JSON.

This allows the backend to deserialize the AI response instead of treating it as unstructured text.

The application therefore follows:

```text
Resume Text + optional retrieved local guidance
     │
     ▼
Groq (default) or local Gemma 3 via Ollama
     │
     ▼
Structured JSON
     │
     ▼
ResumeAnalysisResponse
     │
     ▼
Angular Results UI
```

---

## 🦙 Optional Ollama Setup

Ollama is optional when using Groq. Install Ollama only if you want local Gemma inference or to enable the optional local RAG embeddings:

https://ollama.com/

Verify installation:

```bash
ollama --version
```

Pull Gemma 3:

```bash
ollama pull gemma3
```

Check installed models:

```bash
ollama list
```

Run Gemma 3 manually:

```bash
ollama run gemma3
```

Ollama exposes its local API at:

```text
http://localhost:11434
```

### Local RAG setup

CareerIQ can retrieve relevant passages from its local resume-guidance Markdown files and supply them as AI context. Resume extraction still happens first; RAG augments analysis and does not replace extraction. RAG is disabled by default so normal Groq analysis does not wait on a local Ollama embedding model.

Pull the local embedding model in addition to Gemma:

```bash
ollama pull nomic-embed-text
```

To enable RAG, pull the embedding model above and set `Rag:Enabled` to `true`. Add or edit `.md` files under `ResumeIQ.API/KnowledgeBase/`. The API chunks their `##` sections, creates embeddings through Ollama's `/api/embed` endpoint, and ranks passages by cosine similarity. The in-memory vector index is built on the first analysis request after the API starts; restart the API after changing guidance files. `Rag:EmbeddingModel`, `Rag:TopK`, and `Rag:Enabled` are configurable in `appsettings.json`.

When enabled, RAG sends the resume text to the local Ollama embedding model to find relevant guidance, then includes the retrieved passages in the selected analysis provider's prompt. With Groq selected, the analysis request (including the resume) is still sent to Groq. No vector database or permanent resume/vector storage is used.

## ⚡ AI Provider (Groq)

The default provider uses Groq's hosted `openai/gpt-oss-20b` model for resume analysis. Keep the API key out of source control and chat. To set it without typing it in a terminal:

1. Open `ResumeIQ.API/.env` in VS Code (create it by copying `.env.example` if it is missing).
2. Paste your key after `GROQ_API_KEY=` and save the file.
3. Restart the backend. The API reads this ignored local file at startup; it is not committed to Git.

The backend sends extracted resume text to Groq for analysis; choose resumes you are comfortable submitting to that external service. Uploaded files are processed in memory by CareerIQ and are not permanently stored by the API. Groq analysis requests have a 90-second timeout. To use local Ollama analysis instead, set `Ai:Provider` to `Ollama`; this requires the `Ollama:Model` to be installed and running locally. No Groq key is needed in that mode.

---

## ⚙️ AI Configuration

The backend uses Groq by default. Ollama settings are used for optional local inference and RAG embeddings.

Example configuration:

```json
{
  "Ai": {
    "Provider": "Groq"
  },
  "Groq": {
    "BaseUrl": "https://api.groq.com",
    "Model": "openai/gpt-oss-20b"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "gemma3"
  },
  "Rag": {
    "Enabled": false,
    "EmbeddingModel": "nomic-embed-text",
    "TopK": 3
  }
}
```

`Groq:ApiKey` is intentionally not stored in `appsettings.json`. Add the key to the ignored `ResumeIQ.API/.env` file using the `GROQ_API_KEY=` setting; `ResumeIQ.API/.env.example` is a safe template with an empty value. Restart the backend after changing the key. An environment variable named `Groq__ApiKey` can also be used.

---

## 💻 Prerequisites

Before running the project, install:

- .NET SDK
- Node.js
- Angular CLI
- Git
- A Groq API key (or Ollama if using local inference)

Verify .NET:

```bash
dotnet --version
```

Verify Node:

```bash
node --version
```

Verify Angular:

```bash
ng version
```

Verify Ollama only if using local inference or enabling RAG:

```bash
ollama --version
```

---

## ▶️ Run the Backend

Open a terminal and navigate to the backend:

```bash
cd ResumeIQ.API
```

Restore packages:

```bash
dotnet restore
```

Build:

```bash
dotnet build
```

Run backend extraction and endpoint tests from the repository root:

```bash
dotnet test ResumeIQ.API.Tests/ResumeIQ.API.Tests.csproj
```

Run:

```bash
dotnet run
```

The API will run on the configured local development port.

For the current development configuration:

```text
http://localhost:5155
```

---

## 📖 Swagger

Once the backend is running, open:

```text
http://localhost:5155/swagger
```

The Swagger interface allows the API to be tested without the Angular frontend.

Use:

```text
POST /api/Resume/analyze
POST /api/Resume/analyze-file
```

---

## 🅰️ Run the Angular Frontend

Open another terminal.

Navigate to the frontend:

```bash
cd resume-iq-ui
```

Install dependencies:

```bash
npm install
```

Start Angular:

```bash
ng serve
```

Open:

```text
http://localhost:4200
```

The Resume Analyzer page is available at:

```text
http://localhost:4200/analyze
```

The analyzer accepts PDF, DOCX, legacy DOC, and CSV files up to 10 MB. Alternatively, expand the text-entry option to use the original text-analysis endpoint.

---

## 🔐 CORS

The Angular frontend and ASP.NET Core backend run on different local ports.

```text
Angular
http://localhost:4200

        │
        │ CORS
        ▼

ASP.NET Core
http://localhost:5155
```

The backend allows the Angular development origin through CORS.

---

## ⏳ Loading State

The Angular application maintains a loading state while the configured AI provider processes the resume.

```text
User clicks Analyze
        │
        ▼
isLoading = true
        │
        ▼
Analyze Resume
        │
        ▼
AI processing
        │
        ▼
Response received
        │
        ▼
isLoading = false
        │
        ▼
Display results
```

Angular Signals are used to maintain reactive UI state.

---

## ❌ Error Handling

The Angular frontend handles API errors and displays an appropriate error message to the user.

The backend also provides HTTP status information when API processing fails.

The application therefore handles:

- Empty resume input
- API failures
- AI service failures
- Invalid AI responses
- Network failures

---

## 💰 Cost

The Angular and ASP.NET Core applications are free to run locally. Groq analysis uses a hosted service and may be subject to account limits or charges; check Groq's current pricing and plan. Local Ollama inference does not use a hosted inference API.

| Technology | Cost |
|---|---|
| Angular | Free |
| ASP.NET Core | Free |
| SCSS | Free |
| Ollama | Free / Local |
| Gemma 3 | Free / Local |
| Groq API | Subject to Groq account limits and pricing |
| Git | Free |
| GitHub | Free |

### AI Cost

```text
Groq-hosted analysis (default)  → Uses Groq account quota/pricing
Ollama + Gemma 3 local analysis → Local inference; no Groq analysis call
Optional RAG embeddings         → Local Ollama; disabled by default
```

Choose local Ollama if you need to avoid sending resume text to a hosted AI provider.

---

## 🚧 Current Project Status

### Version 1 — Completed

- [x] ASP.NET Core Web API
- [x] Angular frontend
- [x] Responsive CareerIQ UI
- [x] SCSS styling
- [x] Configurable AI provider selection
- [x] Groq hosted analysis provider (default)
- [x] Ollama integration
- [x] Gemma 3 integration
- [x] Optional local RAG over Markdown resume guidance
- [x] Optional Ollama embeddings and cosine-similarity retrieval
- [x] AI resume analysis
- [x] Structured JSON response
- [x] AI service abstraction
- [x] Dependency Injection
- [x] HttpClientFactory
- [x] Swagger/OpenAPI
- [x] CORS configuration
- [x] Angular to ASP.NET Core integration
- [x] ASP.NET Core to configured AI provider integration
- [x] PDF, DOCX, legacy DOC, and CSV upload/extraction
- [x] 10 MB upload limit and server-side file signature validation
- [x] Image-only PDF detection with a clear no-readable-text response (OCR is not included)
- [x] AI-generated skills
- [x] AI-generated strengths
- [x] AI-generated weaknesses
- [x] AI-generated recommendations
- [x] Loading state
- [x] Error handling
- [x] Responsive analysis dashboard
- [x] Angular Signals
- [x] Structured result rendering
- [x] Groq response schema validation and provider error reporting
- [x] Local ignored `.env` key configuration

### Verification

- Last local verification recorded: **October 30, 2026**.
- Backend Release build: `dotnet build ResumeIQ.API/ResumeIQ.API.csproj --configuration Release` — **passed with 0 warnings and 0 errors**.
- Backend (run from repository root): `dotnet test ResumeIQ.API.Tests/ResumeIQ.API.Tests.csproj --configuration Release` — **21 tests passed**.
- Frontend (run from `resume-iq-ui`): `npm test -- --watch=false` — **13 tests passed**.
- Frontend (run from `resume-iq-ui`): `npm run build` — **passed**, including Angular SSR/prerender build.
- These are the latest recorded local test/build results. Live Groq calls require a valid developer key and network connection; a successful build/test does not replace testing against the developer's own Groq account.
- PDF, DOCX, and CSV extraction are covered by automated tests. Legacy DOC malformed-input rejection is tested; a valid legacy DOC fixture was not available for a positive extraction test.

---

# 🔮 Future Roadmap

## Phase 2 — Resume File Upload (Completed)

- [x] PDF, DOCX, legacy DOC, and CSV upload
- [x] Text extraction and content validation
- [x] 10 MB file size validation and clear errors
- [x] Drag-and-drop, loading state, and duplicate-submit protection
- [x] Paste-text fallback using the original text endpoint

Implemented flow:

```text
PDF / DOCX
     │
     ▼
ASP.NET Core
     │
     ▼
Text Extraction
     │
     ▼
Resume Text
     │
     ▼
Groq (default) or local Ollama
     │
     ▼
Resume Analysis and Results
```

---

## Phase 3 — Database

Add a lightweight local database such as SQLite.

Planned technologies:

- Entity Framework Core
- SQLite
- Repository/service patterns

Planned functionality:

- [ ] Store resumes
- [ ] Store analysis results
- [ ] Analysis history
- [ ] User resume history
- [ ] Delete previous analyses

Architecture:

```text
Angular
   │
   ▼
ASP.NET Core
   │
   ▼
Entity Framework Core
   │
   ▼
SQLite
```

---

## Phase 4 — Job Description Matching

Allow users to provide a job description along with their resume.

```text
Resume
   +
Job Description
   │
   ▼
CareerIQ
   │
   ▼
Job Match Analysis
```

Potential functionality:

- [ ] Skill matching
- [ ] Missing skill detection
- [ ] Keyword matching
- [ ] Job compatibility score
- [ ] Required skill detection
- [ ] Recommended skills
- [ ] Resume improvement suggestions

---

## Phase 5 — Resume Scoring

Introduce a deterministic resume scoring engine.

Instead of relying completely on the LLM, CareerIQ can combine:

```text
               Resume
                  │
        ┌─────────┴─────────┐
        │                   │
        ▼                   ▼
Deterministic Engine    AI Engine
        │                   │
        │                 Gemma
        │                   │
        └─────────┬─────────┘
                  │
                  ▼
            Final Analysis
```

The deterministic engine can handle:

- Skill matching
- Keyword matching
- Section validation
- Required skill detection
- Numeric scoring

The AI can handle:

- Contextual analysis
- Resume quality
- Experience analysis
- Recommendations

---

## Phase 6 — Authentication

Add user authentication.

Potential technologies:

- JWT
- ASP.NET Core Authentication
- Angular Route Guards
- Angular HTTP Interceptor

Planned functionality:

- [ ] Registration
- [ ] Login
- [ ] JWT authentication
- [ ] Password hashing
- [ ] Protected APIs
- [ ] User-specific resume history

Architecture:

```text
Angular
   │
   ▼
Login
   │
   ▼
ASP.NET Core
   │
   ▼
JWT
   │
   ▼
Protected APIs
```

---

## Phase 7 — Advanced AI

A first local RAG implementation is available but is **disabled by default**:

- [x] Ollama `nomic-embed-text` embeddings for local resume-guidance passages
- [x] Semantic retrieval with cosine similarity and configurable top-k
- [x] Retrieved context added to the selected provider's analysis prompt
- [x] Local Markdown knowledge base under `ResumeIQ.API/KnowledgeBase/`
- [x] In-memory vector index; no database or permanent resume/vector storage

With Groq selected, enabling RAG uses Ollama locally to embed the resume query and guidance, then sends the retrieved guidance and resume to Groq for analysis. To keep analysis entirely local, set `Ai:Provider` to `Ollama`; this uses local Gemma 3. See the setup section above before enabling either Ollama path.

Future improvements for larger or more dynamic knowledge collections could include:

- [ ] Persistent vector storage
- [ ] Job-description and role-specific knowledge collections
- [ ] Prompt versioning
- [ ] AI response evaluation

Potential future architecture:

```text
Resume
   │
   ▼
Text Processing
   │
   ▼
Ollama Embeddings
   │
   ▼
In-memory Vector Index
   │
   ▼
Semantic Search
   │
   ▼
RAG
   │
   ▼
Selected Analysis Provider
   │
   ▼
Context-Aware Analysis
```

---

## Phase 8 — Production Engineering

Future backend improvements:

- [ ] Global exception handling
- [ ] Structured logging
- [ ] Request validation
- [ ] Rate limiting
- [ ] Caching
- [ ] Integration testing
- [ ] Positive legacy DOC extraction fixture and test
- [ ] API performance optimization
- [ ] Docker
- [ ] CI/CD
- [ ] Health checks
- [ ] API versioning

---

# 🎯 Learning Objectives

This project is designed to provide practical experience in both **full-stack development and AI integration**.

## ASP.NET Core

- REST API development
- Controllers
- Dependency Injection
- Service abstraction
- HttpClientFactory
- DTOs
- Configuration
- CORS
- Swagger
- JSON serialization/deserialization
- Error handling

## Angular

- Standalone components
- TypeScript
- Angular Signals
- Angular HttpClient
- RxJS
- Dependency Injection
- Component architecture
- Two-way binding
- Loading state
- Error state
- Responsive component styling
- SCSS

## AI

- Groq Chat Completions
- Optional local LLM integration with Ollama and Gemma 3
- Optional Ollama embeddings and RAG
- Prompt engineering
- Structured AI output
- JSON response processing
- LLM service abstraction
- Local inference option

## Future AI Concepts

- Persistent vector storage for larger knowledge collections
- Job-description retrieval
- Prompt optimization
- AI evaluation

---

# 🧠 Why This Project Is Useful

CareerIQ is intentionally designed as a relatively small project while still demonstrating several concepts that are useful in real-world software engineering.

Instead of building a very large application, the project focuses on a complete end-to-end workflow:

```text
Frontend
   ↓
REST API
   ↓
Service Layer
   ↓
External/Local AI Runtime
   ↓
LLM
   ↓
Structured Response
   ↓
Frontend Rendering
```

This makes it suitable for:

- Portfolio demonstration
- ASP.NET Core practice
- Angular practice
- AI integration learning
- System design discussions
- Full-stack interview preparation

---

# 🚀 Future Vision

The long-term goal is to evolve CareerIQ from a basic resume analyzer into a complete **AI-powered Resume Intelligence Platform**.

The planned evolution is:

```text
Resume Analyzer
       │
       ▼
Resume File Processing
       │
       ▼
Groq default / optional local Ollama
       │
       ▼
Resume Scoring
       │
       ▼
Job Description Matching
       │
       ▼
Semantic Skill Matching
       │
       ▼
Ollama Embeddings
       │
       ▼
Optional RAG Retrieval
       │
       ▼
RAG
       │
       ▼
AI Resume Intelligence
```

The current implementation includes optional RAG over curated resume guidance. Persistent vector storage, job-description matching, and resume scoring remain future work.

---

# 👨‍💻 Author

**Tapan Ray**

Software Engineer

**Primary Technologies:**

- C#
- ASP.NET Core
- Angular
- SQL Server
- REST APIs
- Azure
- AI / LLM Integration

---

# 📄 License

This project is created for educational, learning, portfolio, and demonstration purposes.

---

## ⭐ CareerIQ

Built with:

**Angular + ASP.NET Core + Groq** (optional local Ollama + Gemma 3)

An AI-powered resume analysis application with configurable inference.
