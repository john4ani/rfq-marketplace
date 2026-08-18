import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { AssistedDraft, AssistedIntakeAnswer, AssistedIntakeAvailability, AssistedIntakeStatus } from './assisted-intake.service';
import { Rfq, RfqDraft } from './rfq.service';

// Construction Projects intake: a distinct workflow from the general assisted intake. It understands the project first
// (projectSummary) and derives the shared Procurement Need on approve, so it reuses the general draft/answer shapes.
export interface ConstructionIntakeResult {
  sessionId: string;
  initialInput: string;
  status: AssistedIntakeStatus;
  provider: string;
  model: string;
  promptVersion: string;
  projectSummary: string;
  draft: AssistedDraft;
  assumptions: string[];
  uncertainties: string[];
  clarification: { field: string; question: string; choices: string[] } | null;
  answers: AssistedIntakeAnswer[];
  inputTokens: number;
  outputTokens: number;
  approvedRfqId: string | null;
}

@Injectable({ providedIn: 'root' })
export class ConstructionIntakeService {
  private readonly http = inject(HttpClient);
  availability() { return this.http.get<AssistedIntakeAvailability>('/api/v1/construction-intakes/availability'); }
  start(input: string) { return this.http.post<ConstructionIntakeResult>('/api/v1/construction-intakes', { input }); }
  get(id: string) { return this.http.get<ConstructionIntakeResult>(`/api/v1/construction-intakes/${id}`); }
  clarify(id: string, answer: string) { return this.http.post<ConstructionIntakeResult>(`/api/v1/construction-intakes/${id}/clarifications`, { answer }); }
  approve(id: string, draft: RfqDraft, completedInAdvancedForm = false) { return this.http.post<Rfq>(`/api/v1/construction-intakes/${id}/approve`, { draft, completedInAdvancedForm }); }
}
