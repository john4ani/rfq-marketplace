import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AssistedIntakeService } from '../../core/rfqs/assisted-intake.service';
import { RfqService } from '../../core/rfqs/rfq.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { AssistedIntakeComponent } from './assisted-intake.component';

@Component({ template: '' })
class AdvancedFormStub {}

describe('AssistedIntakeComponent', () => {
  let fixture: ComponentFixture<AssistedIntakeComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AssistedIntakeComponent],
      providers: [
        provideRouter([{ path: 'rfqs/new/advanced', component: AdvancedFormStub }]),
        { provide: AssistedIntakeService, useValue: { availability: () => of({ enabled: true, modelAvailable: false }) } },
        { provide: RfqService, useValue: { publish: vi.fn() } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([]) } }
      ]
    }).compileComponents();
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(AssistedIntakeComponent);
  });

  it('goes directly to the advanced form when AI is unavailable', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    expect(router.url).toBe('/rfqs/new/advanced');
  });
});

describe('AssistedIntakeComponent dashboard handoff', () => {
  it('starts the assisted flow automatically when a dashboard description is supplied', async () => {
    const start = vi.fn().mockReturnValue(of({
      sessionId: 'session-id', initialInput: 'Am nevoie de 2 tone de peleți.', status: 'InProgress', mode: 'OpenAi',
      provider: 'OpenAI', model: 'gpt-5.6-luna', promptVersion: 'test',
      draft: { inputType: 'FreeText', title: 'Peleți', projectName: null, description: null, primaryCategoryId: null,
        deliveryAddress: '', deliveryLocality: '', county: '', supplierSearchRadiusKm: 30,
        preferredDeliveryDate: null, quotationDeadline: '2026-08-10T12:00:00Z', alternativeProductsAccepted: true,
        allowSplit: false, deliveryRequired: false, estimatedBudget: null, buyerNotes: null, lines: [] },
      assumptions: [], uncertainties: [], researchSources: [], clarification: null, answers: [],
      inputTokens: 0, outputTokens: 0, approvedRfqId: null
    }));
    await TestBed.configureTestingModule({
      imports: [AssistedIntakeComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ description: 'Am nevoie de 2 tone de peleți.' }) } } },
        { provide: AssistedIntakeService, useValue: { availability: () => of({ enabled: true, modelAvailable: true }), start } },
        { provide: RfqService, useValue: { publish: vi.fn() } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([]) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AssistedIntakeComponent);
    fixture.detectChanges();

    expect(start).toHaveBeenCalledWith('Am nevoie de 2 tone de peleți.');
  });
});

describe('AssistedIntakeComponent final confirmation', () => {
  it('publishes the approved request directly after explicit confirmation', async () => {
    const ready = {
      sessionId: 'e405b27c-1846-46d1-98d5-5681581149c6', initialInput: 'Am nevoie de cherestea pentru acoperiș.', status: 'ReadyForApproval', mode: 'OpenAi',
      provider: 'OpenAI', model: 'test', promptVersion: 'test',
      draft: { inputType: 'FreeText', title: 'Cherestea pentru acoperiș', projectName: null, description: null, primaryCategoryId: 'wood',
        deliveryAddress: 'Strada Exemplu 1', deliveryLocality: 'Brașov', county: 'Brașov', supplierSearchRadiusKm: 30,
        preferredDeliveryDate: null, quotationDeadline: '2026-08-12T12:00:00Z', alternativeProductsAccepted: true,
        allowSplit: false, deliveryRequired: true, estimatedBudget: null, buyerNotes: null,
        lines: [{ kind: 'Product', name: 'Cherestea', specification: null, quantity: 4, unit: 'CubicMetre', alternativesAccepted: true, categoryId: 'wood' }] },
      assumptions: [], uncertainties: [], researchSources: [], clarification: null, answers: [], inputTokens: 1, outputTokens: 1, approvedRfqId: null
    };
    const approve = vi.fn().mockReturnValue(of({ id: 'rfq-1' }));
    const publish = vi.fn().mockReturnValue(of({ id: 'rfq-1' }));
    await TestBed.configureTestingModule({
      imports: [AssistedIntakeComponent],
      providers: [
        provideRouter([{ path: 'rfqs/:id/offers', component: AdvancedFormStub }]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ session: ready.sessionId }) } } },
        { provide: AssistedIntakeService, useValue: { availability: () => of({ enabled: true, modelAvailable: true }), get: () => of(ready), approve } },
        { provide: RfqService, useValue: { publish } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([{ id: 'wood', name: 'Lemn', parentId: null }]) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AssistedIntakeComponent);
    fixture.detectChanges();
    const publishButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Confirmă și publică'))!;
    publishButton.click();
    await fixture.whenStable();

    expect(approve).toHaveBeenCalledOnce();
    expect(publish).toHaveBeenCalledWith('rfq-1');
    expect(TestBed.inject(Router).url).toBe('/rfqs/rfq-1/offers');
  });
});
