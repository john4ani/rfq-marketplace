import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AssistedIntakeService } from '../../core/rfqs/assisted-intake.service';
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
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([]) } }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(AssistedIntakeComponent);
    fixture.detectChanges();

    expect(start).toHaveBeenCalledWith('Am nevoie de 2 tone de peleți.');
  });
});
