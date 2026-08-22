import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SupplierDiscoveryService } from '../../core/supplier-discovery/supplier-discovery.service';
import { SupplierDiscoveryAdminComponent } from './supplier-discovery-admin.component';

describe('SupplierDiscoveryAdminComponent',()=>{
  beforeEach(async()=>{
    await TestBed.configureTestingModule({
      imports:[SupplierDiscoveryAdminComponent],
      providers:[
        provideRouter([]),
        {provide:SupplierDiscoveryService,useValue:{opportunities:()=>of([{
          id:'need-1',title:'Lemn rotund pentru construcții',deliveryLocality:'Marginea',county:'Suceava',
          quotationDeadline:'2026-09-04T13:14:00Z',primaryCategoryId:'category-1',requirementCategoryIds:['category-1']
        }])}}
      ]
    }).compileComponents();
  });

  it('keeps Supplier Discovery need-centric and links to the company registry',()=>{
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.textContent).toContain('Alege cererea pentru care cauți furnizori');
    expect(page.textContent).toContain('Lemn rotund pentru construcții');
    expect(page.textContent).toContain('Control uman obligatoriu');
    expect(page.querySelector('a[href="/admin/companies"]')).not.toBeNull();
  });

  it('does not duplicate the company registry, global validation or invitation flows',()=>{
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.textContent).not.toContain('Coada de validare');
    expect(page.textContent).not.toContain('Adaugă o firmă candidată');
    expect(page.textContent).not.toContain('Pregătește invitație');
  });
});
