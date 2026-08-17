import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { SupplierDiscoveryService } from '../../core/supplier-discovery/supplier-discovery.service';
import { SupplierInvitationComponent } from './supplier-invitation.component';

describe('SupplierInvitationComponent',()=>{
  const invitation={
    invitationId:'invitation-1',candidateId:'candidate-1',companyName:'Lemn Local SRL',
    candidateLabel:'Firmă descoperită extern · date nerevendicate și neverificate de companie',
    status:'Active' as const,expiresAt:'2026-08-19T10:00:00Z',rfqId:'rfq-1',rfqTitle:'Cherestea pentru proiect',
    rfqDescription:'Necesar comercial',deliveryLocality:'Marginea',county:'Suceava',quotationDeadline:'2026-08-12T10:00:00Z',requirementCount:2,claimed:false
  };

  beforeEach(async()=>{
    await TestBed.configureTestingModule({
      imports:[SupplierInvitationComponent],
      providers:[
        provideRouter([]),
        {provide:ActivatedRoute,useValue:{snapshot:{paramMap:{get:()=> 'secure-token'}}}},
        {provide:AuthService,useValue:{isAuthenticated:()=>false,profiles:()=>[],addProfile:()=>of({})}},
        {provide:ProfileService,useValue:{getMyCompanies:()=>of([{id:'company-1',name:'Lemn Local SRL',role:'Owner'}])}},
        {provide:SupplierDiscoveryService,useValue:{invitation:()=>of(invitation),claim:()=>of({...invitation,status:'Claimed',claimed:true})}}
      ]
    }).compileComponents();
  });

  it('shows opportunity value before onboarding and clearly labels unverified company data',()=>{
    const fixture=TestBed.createComponent(SupplierInvitationComponent);
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.querySelector('h1')?.textContent).toContain('cerere relevantă');
    expect(page.textContent).toContain('Cherestea pentru proiect');
    expect(page.textContent).toContain('neverificate de companie');
    expect(page.textContent).toContain('Creează cont de furnizor');
    expect(page.textContent).not.toContain('Adresă privată');
  });

  it('continues a successful claim at the invited opportunity in company context',()=>{
    const router=TestBed.inject(Router);
    const navigate=vi.spyOn(router,'navigate').mockResolvedValue(true);
    const fixture=TestBed.createComponent(SupplierInvitationComponent);
    fixture.componentInstance.claim();

    expect(navigate).toHaveBeenCalledWith(['/opportunities','rfq-1'],{queryParams:{companyId:'company-1'}});
  });
});
