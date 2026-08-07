import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { ProcurementInputType, ProcurementNeedSource, ProcurementNeedStatus, RfqItem } from './rfq.service';
import { TrustFacts } from '../profiles/profile.service';

export interface OpenRfq {
  id: string;
  source: ProcurementNeedSource;
  inputType: ProcurementInputType;
  needStatus: ProcurementNeedStatus;
  projectContext: { name: string | null; description: string | null } | null;
  title: string;
  projectName: string | null;
  description: string | null;
  primaryCategoryId: string;
  categoryName: string;
  buyerDisplayName: string;
  deliveryLocality: string;
  county: string;
  preferredDeliveryDate: string | null;
  quotationDeadline: string;
  alternativeProductsAccepted: boolean;
  deliveryRequired: boolean;
  estimatedBudget: number | null;
  publishedAt: string | null;
  isPartialMatch: boolean;
  items: RfqItem[];
  buyerTrustFacts: TrustFacts | null;
}

@Injectable({ providedIn: 'root' })
export class OpenRfqService {
  private readonly http = inject(HttpClient);

  list(search = '', categoryId = '') {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    if (categoryId) params = params.set('categoryId', categoryId);
    return this.http.get<OpenRfq[]>('/api/v1/open-rfqs', { params });
  }

  get(id: string) {
    return this.http.get<OpenRfq>(`/api/v1/open-rfqs/${id}`);
  }
}
