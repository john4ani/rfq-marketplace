import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SupplierSettingsService } from './supplier-settings.service';

describe('SupplierSettingsService', () => {
  it('saves category and delivery selections for the explicit supplier id', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(SupplierSettingsService);
    const http = TestBed.inject(HttpTestingController);
    const id = '11111111-1111-1111-1111-111111111111';

    service.addCategory('Piatră naturală').subscribe();
    const newCategory = http.expectOne('/api/v1/categories');
    expect(newCategory.request.method).toBe('POST');
    expect(newCategory.request.body).toEqual({ name: 'Piatră naturală' });
    newCategory.flush({ id: 'category-id', parentId: 'root-id', name: 'Piatră naturală', slug: 'piatra-naturala' });

    service.saveSupplierCategories(id, ['category-id']).subscribe();
    const categories = http.expectOne(`/api/v1/suppliers/${id}/categories`);
    expect(categories.request.method).toBe('PUT');
    expect(categories.request.body).toEqual({ categoryIds: ['category-id'] });
    categories.flush({ categoryIds: ['category-id'] });

    const settings = { normalDeliveryRadiusKm: 50, maximumDeliveryRadiusKm: 100, minimumOrderValue: null, nationwideDelivery: false, counties: ['Iași'] };
    service.saveDeliverySettings(id, settings).subscribe();
    const delivery = http.expectOne(`/api/v1/suppliers/${id}/delivery-settings`);
    expect(delivery.request.method).toBe('PUT');
    expect(delivery.request.body).toEqual(settings);
    delivery.flush(settings);
    http.verify();
  });
});
