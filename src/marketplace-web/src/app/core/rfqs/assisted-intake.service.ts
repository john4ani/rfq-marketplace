import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { ProcurementInputType, RequirementKind, Rfq, RfqDraft, UnitOfMeasure } from './rfq.service';

export type AssistedIntakeStatus = 'InProgress' | 'ReadyForApproval' | 'Approved';
export interface AssistedLine { kind: RequirementKind; name: string; specification: string | null; quantity: number | null; unit: UnitOfMeasure | null; alternativesAccepted: boolean; categoryId: string | null; }
export interface AssistedDraft { inputType: ProcurementInputType; title: string; projectName: string | null; description: string | null; primaryCategoryId: string | null; deliveryAddress: string; deliveryLocality: string; county: string; supplierSearchRadiusKm: number; preferredDeliveryDate: string | null; quotationDeadline: string; alternativeProductsAccepted: boolean; allowSplit: boolean; deliveryRequired: boolean; estimatedBudget: number | null; buyerNotes: string | null; lines: AssistedLine[]; }
export interface AssistedIntakeAnswer { field: string; question: string; answer: string; }
export interface AssistedResearchSource { title: string; url: string; finding: string; }
export type SuggestedProcurementMode = 'GeneralProcurement' | 'ConstructionProjects' | 'SeapTenderSearch';
export interface AssistedIntakeResult { sessionId: string; initialInput: string; status: AssistedIntakeStatus; mode: 'OpenAi'; provider: string; model: string; promptVersion: string; draft: AssistedDraft; assumptions: string[]; uncertainties: string[]; researchSources: AssistedResearchSource[]; clarification: { field: string; question: string; choices: string[] } | null; answers: AssistedIntakeAnswer[]; inputTokens: number; outputTokens: number; approvedRfqId: string | null; suggestedMode?: SuggestedProcurementMode | null; }
export interface AssistedIntakeAvailability { enabled: boolean; modelAvailable: boolean; }

@Injectable({ providedIn: 'root' })
export class AssistedIntakeService {
  private readonly http = inject(HttpClient);
  availability() { return this.http.get<AssistedIntakeAvailability>('/api/v1/assisted-intakes/availability'); }
  start(input: string) { return this.http.post<AssistedIntakeResult>('/api/v1/assisted-intakes', { input }); }
  get(id: string) { return this.http.get<AssistedIntakeResult>(`/api/v1/assisted-intakes/${id}`); }
  clarify(id: string, answer: string) { return this.http.post<AssistedIntakeResult>(`/api/v1/assisted-intakes/${id}/clarifications`, { answer }); }
  approve(id: string, draft: RfqDraft, completedInAdvancedForm = false) { return this.http.post<Rfq>(`/api/v1/assisted-intakes/${id}/approve`, { draft, completedInAdvancedForm }); }
}
