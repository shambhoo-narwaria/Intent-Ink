# Ollama Model Setup & Prompt Engineering

> **Local Model Selection, Prompt Engineering, Structured JSON Contracts, and Validation Guardrails**

---

## Setting up Ollama for IntentInk

IntentInk connects directly to [Ollama](https://ollama.com/) running locally on your Windows machine (`http://localhost:11434/`). All data is processed entirely offline on your hardware.

### 1. Download & Install Ollama
Download and run the installer from [ollama.com/download](https://ollama.com/download).

### 2. Recommended Models for Grammar Correction

For fast, interactive typing assistance with low resource consumption, **3B parameter models** are strongly recommended:

| Model | Command | Strengths |
|---|---|---|
| **Llama 3.2 3B** *(Recommended)* | `ollama pull llama3.2:3b` | Outstanding sentence structure repair, high speed (<3s on modern CPUs), excellent adherence to structured schema. |
| **Qwen 2.5 3B** | `ollama pull qwen2.5:3b` | Strong multilingual support, Hinglish handling, and preservation of code identifiers and technical terms. |
| **Mistral 7B** | `ollama pull mistral` | Advanced linguistic nuances if a dedicated GPU (e.g. RTX 3060+) is available. |

### 3. Verify Ollama is Running
In PowerShell:
```powershell
ollama list
```
If the Ollama daemon is not running:
```powershell
ollama serve
```

---

## Specialized System Prompt

IntentInk uses a specialized prompt in `src/IntentInk.Desktop/Infrastructure/OllamaClient.cs` that addresses real-world typing habits:

```text
You are an expert grammar, spelling, phrasing, and sentence structure editor.
Your task is to fix spelling mistakes, grammatical errors, awkward word order, broken sentence structure, missing or trailing words, and punctuation in the input text while preserving the user's intended meaning and tone.
Specifically:
1. Fix incomplete phrasing and dangling trailing words (for example: trailing 'of the', 'to the', or dangling prepositions/articles like 'contains the object properties of the' -> 'contains the object properties.').
2. Fix inverted or scrambled word order (e.g. 'i [name] am' -> 'I am [Name]').
3. Ensure the sentence starts with a capital letter and ends with appropriate terminal punctuation (period, question mark, or exclamation point).
4. Preserve technical terms, code identifiers, and proper names (capitalize names appropriately).
5. Do not answer questions or add commentary; output only the corrected sentence.
6. If the text is already 100% grammatically perfect and complete, return it unchanged.
Return only an object matching the supplied schema with corrected_text.
```

---

## JSON Structured Output Contract

To ensure deterministic parsing and prevent markdown code fences (` ``` `) from corrupting output, IntentInk enforces Ollama's native JSON Schema:

```json
{
  "model": "llama3.2:3b",
  "stream": false,
  "keep_alive": "5m",
  "options": {
    "temperature": 0,
    "num_predict": 1024
  },
  "format": {
    "type": "object",
    "properties": {
      "corrected_text": { "type": "string" }
    },
    "required": ["corrected_text"],
    "additionalProperties": false
  },
  "messages": [
    { "role": "system", "content": "<SYSTEM_PROMPT>" },
    { "role": "user", "content": "{\"text\":\"Just add it to the server that contains the object properties of the\"}" }
  ]
}
```

### Response
```json
{
  "corrected_text": "Just add it to the server that contains the object properties."
}
```

---

## Timeout & Concurrency Safeguards

1. **45-Second HTTP Timeout**: Accommodates local LLMs during initial cold-start or heavy CPU utilization without aborting prematurely.
2. **Cancellation Discrimination**: Explicitly distinguishes between user-initiated cancellations (e.g. clicking Cancel or clearing selection) and true HTTP network timeouts in logs.

---

## Post-Inference Validation Checks (`src/IntentInk.Core/Services/CorrectionValidator.cs`)

Before any suggestion is displayed to the user:
1. **Identical Text Detection**: If `corrected_text == original_text`, the UI presents the `No Changes Needed` state instead of vanishing.
2. **Length Guard**: Outputs exceeding 3,000 characters are suppressed.
3. **Edit-Ratio Guard**: If the Levenshtein edit distance exceeds 80% of original text length, the suggestion is rejected (guards against hallucinatory rewrites).
4. **Protected Token Invariance**:
   - URLs (`https?://\S+`)
   - Email addresses (`\b[\w.+-]+@[\w-]+\.\w{2,}\b`)
   - Semantic version numbers (`\bv?\d+\.\d+(\.\d+)*\b`)  
   Must be preserved identically in value and sequence.
