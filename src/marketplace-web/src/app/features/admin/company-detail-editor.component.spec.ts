import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CompanyRegistryService } from '../../core/companies/company-registry.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { CompanyDetailEditorComponent } from './company-detail-editor.component';

describe('CompanyDetailEditorComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyDetailEditorComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: (key: string) => key === 'id' ? 'candidate-1' : 'Discovered' } } } },
        { provide: CompanyRegistryService, useValue: { list: () => of([{
          id: 'candidate-1', name: 'Firma găsită SRL', kind: 'Discovered', claimed: false, locality: 'Marginea', county: 'Suceava',
          email: 'contact@firma.example', phone: null, website: null, taxIdentifier: null, categories: ['Lemn și plăci'],
          relevantOpportunities: 1, activeOpportunities: 1, approvedMatches: 0, lastRelevantOpportunity: null, createdAt: '2026-08-22T00:00:00Z', contactState: 'Nerevendicată', isActive: false, freeGenerationsRemaining: 0
        }]) } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([{ id: 'category-1', name: 'Lemn și plăci' }]) } }
      ]
    }).compileComponents();
  });

  it('allows an administrator to open a prefilled editor for an unclaimed discovered company', () => {
    const fixture = TestBed.createComponent(CompanyDetailEditorComponent);
    fixture.detectChanges();
    const button = [...fixture.nativeElement.querySelectorAll('button')].find((item: HTMLButtonElement) => item.textContent?.includes('Editează datele firmei')) as HTMLButtonElement;

    button.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.form.getRawValue().companyName).toBe('Firma găsită SRL');
    expect(fixture.nativeElement.textContent).toContain('Salvează modificările');
  });

  it('shows the free-generation grant action for a registered company and calls the registry', () => {
    const registeredEntry = {
      id: 'reg-1', name: 'Firmă înregistrată SRL', kind: 'Registered' as const, claimed: true, locality: 'Suceava', county: 'Suceava',
      email: 'contact@firma.example', phone: null, website: null, taxIdentifier: 'RO123', categories: ['Lemn și plăci'],
      relevantOpportunities: 2, activeOpportunities: 1, approvedMatches: 1, lastRelevantOpportunity: null, createdAt: '2026-08-22T00:00:00Z', contactState: null, isActive: true, freeGenerationsRemaining: 0
    };
    const grantFreeGeneration = vi.fn().mockReturnValue(of({ ...registeredEntry, freeGenerationsRemaining: 1 }));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [CompanyDetailEditorComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: (key: string) => key === 'id' ? 'reg-1' : 'Registered' } } } },
        { provide: CompanyRegistryService, useValue: { list: () => of([registeredEntry]), grantFreeGeneration } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([{ id: 'category-1', name: 'Lemn și plăci' }]) } }
      ]
    });
    const fixture = TestBed.createComponent(CompanyDetailEditorComponent);
    fixture.detectChanges();

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const button = [...fixture.nativeElement.querySelectorAll('button')].find((item: HTMLButtonElement) => item.textContent?.includes('Oferă o generare gratuită')) as HTMLButtonElement;
    expect(button).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('0');

    button.click();
    fixture.detectChanges();

    expect(grantFreeGeneration).toHaveBeenCalledWith('reg-1');
    expect(fixture.nativeElement.textContent).toContain('1');
    confirmSpy.mockRestore();
  });
});
