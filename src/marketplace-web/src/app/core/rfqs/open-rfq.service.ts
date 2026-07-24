import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { RfqItem } from './rfq.service';

export interface OpenRfq {
  id: string;
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
  estimatedBudget: number | null;
  publishedAt: string | null;
  isPartialMatch: boolean;
  items: RfqItem[];
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
