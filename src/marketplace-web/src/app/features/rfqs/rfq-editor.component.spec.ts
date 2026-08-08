import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AssistedIntakeService } from '../../core/rfqs/assisted-intake.service';
import { RfqService } from '../../core/rfqs/rfq.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { RfqEditorComponent } from './rfq-editor.component';

describe('RfqEditorComponent material positions', () => {
  let buyerProfileResponse: Observable<unknown>;

  beforeEach(async () => {
    buyerProfileResponse = of({ defaultCity: 'Iași', defaultCounty: 'Iași', marketRadiusKm: 30 });
    await TestBed.configureTestingModule({
      imports: [RfqEditorComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: () => ({ userId: 'buyer-id', email: 'buyer@example.com', accountType: 'Buyer' }) } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([]), getProjectTemplates: () => of([]) } },
        { provide: ProfileService, useValue: { getBuyer: () => buyerProfileResponse } },
        { provide: AssistedIntakeService, useValue: {} },
        { provide: RfqService, useValue: {} }
      ]
    }).compileComponents();
  });

  afterEach(() => history.replaceState({}, ''));

  it('clears a stale AI fallback message after the assisted draft loads', () => {
    history.replaceState({ message: 'Asistentul AI este temporar indisponibil.' }, '');
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: {
      paramMap: convertToParamMap({}), queryParamMap: convertToParamMap({ assistedSession: 'session-id' })
    } } });
    TestBed.overrideProvider(AssistedIntakeService, { useValue: { get: () => of({
      sessionId: 'session-id', initialInput: 'Test', status: 'InProgress', mode: 'OpenAi',
      provider: 'OpenAI', model: 'test-model', promptVersion: 'test-v1', approvedRfqId: null,
      assumptions: [], uncertainties: [], researchSources: [], clarification: null, answers: [],
      inputTokens: 0, outputTokens: 0,
      draft: {
        inputType: 'FreeText', title: 'Draft asistat', projectName: null, description: null,
        primaryCategoryId: null, deliveryAddress: 'Strada Test 1', deliveryLocality: 'Iași', county: 'Iași',
        supplierSearchRadiusKm: 30, preferredDeliveryDate: null,
        quotationDeadline: new Date(Date.now() + 172800000).toISOString(),
        alternativeProductsAccepted: true, allowSplit: false, deliveryRequired: true,
        estimatedBudget: null, buyerNotes: null, lines: []
      }
    }) } });

    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.message()).toBe('');
    expect(history.state.message).toBeUndefined();
    expect(fixture.componentInstance.form.controls.title.value).toBe('Draft asistat');
  });

  it('redirects a Buyer without completed Buyer details to the relevant profile form', () => {
    buyerProfileResponse = throwError(() => new HttpErrorResponse({ status: 404 }));
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/dashboard'], {
      queryParams: { section: 'settings', returnUrl: '/rfqs/new' }
    });
  });

  it('collapses a completed position and focuses the newly added one', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.categories.set([{id:'category-id',verticalId:'vertical-id',parentId:'root-id',name:'Ciment și mortare',slug:'ciment'}]);
    component.toggleNeedCategory('category-id');

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.add-item')?.click();
    fixture.detectChanges();
    component.items.at(0).controls['productName'].setValue('Ciment 40 kg');
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.add-item')?.click();
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));

    const positions = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLDetailsElement>('details.item');
    expect(positions.length).toBe(2);
    expect(positions[0].open).toBe(false);
    expect(positions[0].querySelector('summary')?.textContent).toContain('Ciment 40 kg');
    expect(positions[1].open).toBe(true);
    expect(document.activeElement).toBe(positions[1].querySelector('input[formControlName="productName"]'));
  });

  it('keeps an incomplete current position open instead of adding another', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.addItem();
    component.addItem();
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));

    expect(component.items.length).toBe(1);
    expect(component.expandedItemIndex()).toBe(0);
    expect(component.itemMessage()).toContain('Completează denumirea');
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLDetailsElement>('details.item')?.open).toBe(true);
  });

  it('captures a service as an explicit requirement-line kind', () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();

    fixture.componentInstance.addItem({ productName: 'Montaj', quantity: 1, unit: 'Other', kind: 'Service' });
    fixture.detectChanges();

    expect(fixture.componentInstance.items.at(0).controls['kind'].value).toBe('Service');
    expect(fixture.componentInstance.kindLabel('Service')).toBe('Serviciu');
  });

  it('lists exact validation errors and focuses the first invalid field', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.save(false);
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));

    expect(component.validationErrors()).toContain('Completează titlul cererii.');
    expect(component.validationErrors()).toContain('Selectează cel puțin o categorie pentru necesar.');
    const title = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[formControlName="title"]');
    expect(title?.classList.contains('ng-invalid')).toBe(true);
    expect(title?.classList.contains('ng-touched')).toBe(true);
    expect(document.activeElement).toBe(title);
    expect((fixture.nativeElement as HTMLElement).querySelector('.field-error')?.textContent).toContain('Completează titlul cererii.');
  });

  it('requires a valid request before showing the explicit publish review', () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.openPreview();
    expect(component.previewing()).toBe(false);

    component.form.patchValue({
      title: 'Peleți pentru iarnă', primaryCategoryId: 'category-id', deliveryAddress: 'Strada Test 1',
      deliveryLocality: 'Iași', county: 'Iași'
    });
    component.selectedCategoryIds.set(['category-id']);
    component.openPreview();
    fixture.detectChanges();

    expect(component.previewing()).toBe(true);
    expect((fixture.nativeElement as HTMLElement).querySelector('.request-preview')?.textContent).toContain('Trimite cererea');
  });

  it('opens a collapsed invalid position before focusing its field', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.form.patchValue({
      title: 'Cerere test',
      primaryCategoryId: 'category-id',
      deliveryAddress: 'Strada Test 1',
      deliveryLocality: 'Iași',
      county: 'Iași'
    });
    component.selectedCategoryIds.set(['category-id']);
    component.addItem();
    component.expandedItemIndex.set(null);
    component.save(false);
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));

    expect(component.validationErrors()).toContain('Poziția 1: completează denumirea.');
    expect(component.expandedItemIndex()).toBe(0);
    const position = (fixture.nativeElement as HTMLElement).querySelector<HTMLDetailsElement>('details.item');
    const productName = position?.querySelector<HTMLInputElement>('input[formControlName="productName"]');
    expect(position?.open).toBe(true);
    expect(document.activeElement).toBe(productName);
  });
});
