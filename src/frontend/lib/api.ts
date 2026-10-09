const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
    credentials: "include",
  });

  if (response.status === 204) {
    return null as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const payload = contentType.includes("application/json") ? await response.json() : await response.text();
  if (!response.ok) {
    const problem = payload as { message?: string; title?: string; errors?: Record<string, string[]> };
    const validationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
    const message = typeof payload === "string" ? payload : validationMessage ?? problem?.message ?? problem?.title ?? "The request could not be completed.";
    throw new ApiError(response.status, message);
  }

  return payload as T;
}
