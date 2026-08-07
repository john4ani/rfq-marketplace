import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface CategoryOption { id: string; verticalId: string; parentId: string | null; name: string; slug: string; }
export interface CategoryCreationResult extends CategoryOption { alreadyExisted: boolean; }
export interface SupplierCategorySelection { categoryIds: string[]; }
export interface SupplierDeliverySettings {
  normalDeliveryRadiusKm: number;
  maximumDeliveryRadiusKm: number;
  minimumOrderValue: number | null;
  nationwideDelivery: boolean;
  counties: string[];
}
export interface ProjectTemplateOption {
  id: string; verticalId: string; name: string; slug: string; description: string | null; categoryIds: string[];
}

@Injectable({ providedIn: 'root' })
export class SupplierSettingsService {
  private readonly http = inject(HttpClient);
  getCategories() { return this.http.get<CategoryOption[]>('/api/v1/categories'); }
  addCategory(name: string, confirmSimilar = false) {
    return this.http.post<CategoryCreationResult>('/api/v1/categories', { name, confirmSimilar });
  }
  getProjectTemplates() { return this.http.get<ProjectTemplateOption[]>('/api/v1/project-templates'); }
  addProjectTemplate(name: string, description: string | null, categoryIds: string[]) {
    return this.http.post<ProjectTemplateOption>('/api/v1/project-templates', { name, description, categoryIds });
  }
  getSupplierCategories(id: string) { return this.http.get<SupplierCategorySelection>(`/api/v1/suppliers/${id}/categories`); }
  saveSupplierCategories(id: string, categoryIds: string[]) {
    return this.http.put<SupplierCategorySelection>(`/api/v1/suppliers/${id}/categories`, { categoryIds });
  }
  getDeliverySettings(id: string) { return this.http.get<SupplierDeliverySettings>(`/api/v1/suppliers/${id}/delivery-settings`); }
  saveDeliverySettings(id: string, settings: SupplierDeliverySettings) {
    return this.http.put<SupplierDeliverySettings>(`/api/v1/suppliers/${id}/delivery-settings`, settings);
  }
}
