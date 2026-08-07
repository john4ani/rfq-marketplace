import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TrustFacts } from '../../core/profiles/profile.service';

@Component({selector:'app-trust-facts',changeDetection:ChangeDetectionStrategy.OnPush,template:`@if(facts();as trust){<div class="trust-facts" [class.compact]="compact()">@if(trust.companyVerified){<span>✓ Companie verificată</span>}@if(trust.emailVerified){<span>✓ E-mail verificat</span>}@if(trust.phoneVerified){<span>✓ Telefon verificat</span>}@if(role()==='Supplier' && trust.submittedOffers){<span>● {{trust.submittedOffers}} oferte trimise</span>}@if(role()==='Supplier' && trust.confirmedCollaborations){<span>● {{trust.confirmedCollaborations}} colaborări confirmate</span>}@if(role()==='Buyer' && trust.completedRequests){<span>● {{trust.completedRequests}} cereri finalizate</span>}<span>● Membru din {{trust.memberSince | date:'yyyy'}}</span></div>}`,imports:[DatePipe]})
export class TrustFactsComponent { readonly facts=input<TrustFacts|null>(null);readonly role=input<'Buyer'|'Supplier'>('Supplier');readonly compact=input(true); }
