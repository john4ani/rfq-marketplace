import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
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

  it('shows invitation failures beside the action that failed and focuses the request field',async()=>{
    const eligible={...candidate,status:'EligibleForInvitation' as const};
    TestBed.overrideProvider(SupplierDiscoveryService,{useValue:{
      candidates:()=>of([eligible]),
      opportunities:()=>of([{id:'rfq-1',title:'Cherestea',deliveryLocality:'Marginea',county:'Suceava',quotationDeadline:'2026-08-19T10:00:00Z',primaryCategoryId:'category-1'}]),
      createInvitation:()=>throwError(()=>({status:409,error:{detail:'Există deja o invitație activă pentru această firmă și cerere.'}}))
    }});
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    fixture.componentInstance.startInvitation(eligible.id);
    fixture.componentInstance.opportunityId.setValue('rfq-1');
    fixture.componentInstance.createInvitation(eligible);
    fixture.detectChanges();

    await new Promise(resolve=>setTimeout(resolve));
    const page=fixture.nativeElement as HTMLElement;
    const actionError=page.querySelector('#invitation-action-error');
    const requestField=page.querySelector('#invitation-opportunity-candidate-1');
    expect(actionError?.textContent).toContain('Există deja o invitație activă');
    expect(requestField?.getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(requestField);
  });

  it('renders the generated secure link in a focused result panel',()=>{
    const eligible={...candidate,status:'EligibleForInvitation' as const};
    TestBed.overrideProvider(SupplierDiscoveryService,{useValue:{
      candidates:()=>of([eligible]),
      opportunities:()=>of([{id:'rfq-1',title:'Cherestea',deliveryLocality:'Marginea',county:'Suceava',quotationDeadline:'2026-08-19T10:00:00Z',primaryCategoryId:'category-1',requirementCategoryIds:['category-1']}]),
      createInvitation:()=>of({invitationId:'inv-1',token:'secure-token',expiresAt:'2026-08-19T10:00:00Z'})
    }});
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    fixture.componentInstance.startInvitation(eligible.id);
    fixture.componentInstance.opportunityId.setValue('rfq-1');
    fixture.componentInstance.createInvitation(eligible);
    fixture.detectChanges();

    const result=(fixture.nativeElement as HTMLElement).querySelector('#created-invitation');
    const link=result?.querySelector('input') as HTMLInputElement|null;
    expect(link?.value).toContain('/supplier-invitations/secure-token');
  });

  it('prevents an incompatible request from being selected for a candidate',()=>{
    const eligible={...candidate,status:'EligibleForInvitation' as const};
    TestBed.overrideProvider(SupplierDiscoveryService,{useValue:{
      candidates:()=>of([eligible]),
      opportunities:()=>of([{id:'rfq-2',title:'Transport local',deliveryLocality:'Marginea',county:'Suceava',quotationDeadline:'2026-08-19T10:00:00Z',primaryCategoryId:'category-2',requirementCategoryIds:['category-2']}])
    }});
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    fixture.componentInstance.startInvitation(eligible.id);
    fixture.detectChanges();

    const page=fixture.nativeElement as HTMLElement;
    expect(page.querySelectorAll('#invitation-opportunity-candidate-1 option')).toHaveLength(1);
    expect(page.textContent).toContain('Nu există cereri deschise compatibile');
  });

  it('does not offer a sibling category merely because both categories share a parent',()=>{
    const eligible={...candidate,status:'EligibleForInvitation' as const};
    TestBed.overrideProvider(SupplierDiscoveryService,{useValue:{
      candidates:()=>of([eligible]),
      opportunities:()=>of([{id:'rfq-insulation',title:'Izolație',deliveryLocality:'Marginea',county:'Suceava',quotationDeadline:'2026-08-19T10:00:00Z',primaryCategoryId:'insulation',requirementCategoryIds:['insulation']}])
    }});
    TestBed.overrideProvider(SupplierSettingsService,{useValue:{getCategories:()=>of([
      {id:'construction',parentId:null,name:'Construcții',slug:'constructii'},
      {id:'category-1',parentId:'construction',name:'Lemn și plăci',slug:'lemn'},
      {id:'insulation',parentId:'construction',name:'Izolații',slug:'izolatii'}
    ])}});
    const fixture=TestBed.createComponent(SupplierDiscoveryAdminComponent);
    fixture.detectChanges();
    fixture.componentInstance.startInvitation(eligible.id);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('#invitation-opportunity-candidate-1 option')).toHaveLength(1);
  });
});
