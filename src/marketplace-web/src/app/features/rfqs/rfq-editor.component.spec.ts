import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { RfqService } from '../../core/rfqs/rfq.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { RfqEditorComponent } from './rfq-editor.component';

describe('RfqEditorComponent material positions', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RfqEditorComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: () => ({ userId: 'buyer-id', email: 'buyer@example.com', accountType: 'Buyer' }) } },
        { provide: SupplierSettingsService, useValue: { getCategories: () => of([]) } },
        { provide: ProfileService, useValue: { getBuyer: () => of({ defaultCity: 'Iași', defaultCounty: 'Iași', marketRadiusKm: 30 }) } },
        { provide: RfqService, useValue: {} }
      ]
    }).compileComponents();
  });

  it('collapses a completed position and focuses the newly added one', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

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

  it('lists exact validation errors and focuses the first invalid field', async () => {
    const fixture = TestBed.createComponent(RfqEditorComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.save(false);
    fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve));

    expect(component.validationErrors()).toContain('Completează titlul cererii.');
    expect(component.validationErrors()).toContain('Selectează categoria principală.');
    const title = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[formControlName="title"]');
    expect(title?.classList.contains('ng-invalid')).toBe(true);
    expect(title?.classList.contains('ng-touched')).toBe(true);
    expect(document.activeElement).toBe(title);
    expect((fixture.nativeElement as HTMLElement).querySelector('.field-error')?.textContent).toContain('Completează titlul cererii.');
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
