import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
export interface CompanyRegistryEntry { id:string;name:string;kind:'Registered'|'Discovered';claimed:boolean;locality:string;county:string;email:string|null;phone:string|null;website:string|null;taxIdentifier:string|null;categories:string[];relevantOpportunities:number;activeOpportunities:number;approvedMatches:number;lastRelevantOpportunity:string|null;createdAt:string;contactState:string|null; }
@Injectable({providedIn:'root'}) export class CompanyRegistryService { private readonly http=inject(HttpClient); list(){return this.http.get<CompanyRegistryEntry[]>('/api/v1/admin/companies');} }
