import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService, SupplierProfile } from '../../core/profiles/profile.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { CompanyRegistrationComponent } from './company-registration.component';

describe('CompanyRegistrationComponent', () => {
  it('creates the company for the authenticated User without adding a Supplier account', () => {
    const company: SupplierProfile = {
      id: '11111111-1111-1111-1111-111111111111', tradingName: 'Firma Nouă SRL',
      companyRegistrationNumber: 'J33/1/2026', taxIdentifier: null, contactPerson: 'Ion Popescu',
      contactEmail: 'ion@example.test', phoneNumber: '0712345678', website: null,
      address: 'Strada Principală 1', city: 'Suceava', county: 'Suceava', latitude: null,
      longitude: null, description: null, approvalStatus: 'Pending', isActive: true
    };
    const profiles = { saveSupplier: vi.fn().mockReturnValue(of(company)) };
    const auth = {
      identity: () => ({ userId: company.id, email: 'ion@example.test', roles: [] }),
      session: () => ({ userId: company.id, email: 'ion@example.test', roles: [], accountType: 'Buyer' })
    };
    const settings = {
      getCategories: vi.fn().mockReturnValue(of([{ id: 'timber', verticalId: 'general', parentId: null, name: 'Lemn și produse din lemn', slug: 'lemn' }])),
      saveSupplierCategories: vi.fn().mockReturnValue(of({ categoryIds: ['timber'] })),
      saveDeliverySettings: vi.fn().mockReturnValue(of({ normalDeliveryRadiusKm: 50, maximumDeliveryRadiusKm: 100, minimumOrderValue: null, nationwideDelivery: false, counties: ['Suceava'] })),
      getSupplierCategories: vi.fn().mockReturnValue(of({ supplierId: company.id, categoryIds: [] })),
      getDeliverySettings: vi.fn().mockReturnValue(of({ normalDeliveryRadiusKm: 50, maximumDeliveryRadiusKm: 100, minimumOrderValue: null, nationwideDelivery: false, counties: [] }))
    };
    TestBed.configureTestingModule({
      imports: [CompanyRegistrationComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }, { provide: ProfileService, useValue: profiles }, { provide: SupplierSettingsService, useValue: settings }]
    });
    const fixture = TestBed.createComponent(CompanyRegistrationComponent);
    fixture.componentInstance.form.patchValue({
      tradingName: company.tradingName, companyRegistrationNumber: company.companyRegistrationNumber,
      contactPerson: company.contactPerson, contactEmail: company.contactEmail, phoneNumber: company.phoneNumber,
      address: company.address, city: company.city, county: company.county
    });
    fixture.componentInstance.toggleCategory('timber');
    fixture.componentInstance.toggleCounty('Suceava');

    fixture.componentInstance.createCompany();
    fixture.detectChanges();

    expect(profiles.saveSupplier).toHaveBeenCalledWith(company.id, expect.objectContaining({ tradingName: company.tradingName }));
    expect(settings.saveSupplierCategories).toHaveBeenCalledWith(company.id, ['timber']);
    expect(settings.saveDeliverySettings).toHaveBeenCalledWith(company.id, expect.objectContaining({ counties: ['Suceava'], nationwideDelivery: false }));
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Firma Nouă SRL a fost adăugată');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Produse și servicii');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Zonă de lucru');
  });
});
