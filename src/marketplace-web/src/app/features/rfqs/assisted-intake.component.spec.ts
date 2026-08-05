import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
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
