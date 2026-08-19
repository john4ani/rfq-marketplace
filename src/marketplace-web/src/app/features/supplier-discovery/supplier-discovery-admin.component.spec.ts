import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { SupplierDiscoveryService } from '../../core/supplier-discovery/supplier-discovery.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { SupplierDiscoveryAdminComponent } from './supplier-discovery-admin.component';

describe('SupplierDiscoveryAdminComponent',()=>{
  const candidate={
    id:'candidate-1',companyName:'Lemn Local SRL',contactEmail:'contact@example.test',phoneNumber:null,website:null,
    city:'Marginea',county:'Suceava',categories:[{id:'category-1',name:'Lemn și plăci'}],source:'Site verificat manual',notes:null,
    status:'Unverified' as const,createdAt:'2026-08-05T10:00:00Z',reviewNotes:null,claimedBySupplierId:null,invitations:[]
  };

  beforeEach(async()=>{
    await TestBed.configureTestingModule({
      imports:[SupplierDiscoveryAdminComponent],
      providers:[
        provideRouter([]),
        {provide:SupplierDiscoveryService,useValue:{candidates:()=>of([candidate]),opportunities:()=>of([]),createCandidate:()=>of(candidate),reviewCandidate:()=>of({...candidate,status:'EligibleForInvitation'}),createInvitation:()=>of({invitationId:'inv-1',token:'secure-token',expiresAt:'2026-08-19T10:00:00Z'})}},
        {provide:SupplierSettingsService,useValue:{getCategories:()=>of([{id:'category-1',parentId:'root',name:'Lemn și plăci',slug:'lemn'}])}}
      ]
    }).compileComponents();
  });

  it('marks discovered data as unverified and requires a human review action',()=>{
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.textContent).toContain('Control uman obligatoriu');
    expect(page.textContent).toContain('Date neverificate');
    expect(page.textContent).toContain('Verifică relevanța');
    expect(page.textContent).not.toContain('Trimite automat');
  });

  it('sends a reviewed candidate to its approved opportunity acquisition context',()=>{
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    const navigate=vi.spyOn(TestBed.inject(Router),'navigate').mockResolvedValue(true);
    fixture.componentInstance.startInvitation(candidate.id);
    expect(navigate).toHaveBeenCalledWith(['/admin/supplier-discovery/acquisition'],{queryParams:{candidateId:candidate.id}});
  });
});
