import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SupplierCandidate, SupplierDiscoveryService, SupplierInvitationStatus } from '../../core/supplier-discovery/supplier-discovery.service';

@Component({
  selector: 'app-invitations-referrals-admin',
  imports: [DatePipe, RouterLink],
  template: `<section class="page">
    <header><p class="eyebrow">Administrare</p><h1>Invitații și recomandări</h1>
      <p>Urmărește invitațiile securizate pregătite pentru furnizorii recomandați și starea fiecăreia.</p>
      <a routerLink="/admin/supplier-discovery">Creează o invitație din Supplier discovery</a>
    </header>
    @if(loading()){<p class="state" role="status">Se încarcă invitațiile…</p>}
    @else if(error()){<section class="state error" role="alert"><span>{{error()}}</span><button type="button" (click)="load()">Încearcă din nou</button></section>}
    @else if(invitationCount()===0){<section class="empty"><strong>Nu există invitații încă.</strong><span>Adaugă și validează un furnizor în Supplier discovery, apoi creează linkul securizat pentru cererea potrivită.</span><a routerLink="/admin/supplier-discovery">Deschide Supplier discovery</a></section>}
    @else {
    <div class="list">@for(candidate of candidatesWithInvitations();track candidate.id){<article>
      <div class="company"><div><h2>{{candidate.companyName}}</h2><span>{{candidate.city}}, {{candidate.county}} · sursă: {{candidate.source}}</span></div><strong>{{candidate.invitations.length}} invitații</strong></div>
      <ul>@for(invitation of candidate.invitations;track invitation.id){<li><div><strong>{{invitation.rfqTitle}}</strong><span>Creată {{invitation.createdAt|date:'dd.MM.yyyy'}} · expiră {{invitation.expiresAt|date:'dd.MM.yyyy'}}</span></div><span class="status" [class.active]="invitation.status==='Active'">{{statusLabel(invitation.status)}}</span></li>}</ul>
    </article>}</div>}
  </section>`,
  styles: [`.page{display:grid;gap:1.25rem;max-width:72rem}.page header{display:grid;justify-items:start;gap:.35rem}.eyebrow{margin:0;color:#176b52;font-size:.75rem;font-weight:850;letter-spacing:.1em;text-transform:uppercase}h1{margin:0;font-size:clamp(2rem,4vw,3rem)}header p{max-width:46rem;margin:.2rem 0;color:#637169}a{display:inline-flex;padding:.7rem .9rem;border-radius:.6rem;color:#fff;background:#176b52;font-weight:750;text-decoration:none}.state,.empty,article{padding:1rem;border:1px solid #dce5e0;border-radius:1rem;background:#fff}.error{color:#9c2f24;background:#fff0ee}.empty{display:grid;gap:.4rem}.empty span{color:#637169}.empty a{width:max-content;margin-top:.35rem}.list{display:grid;gap:1rem}article{display:grid;gap:.9rem}.company{display:flex;align-items:start;justify-content:space-between;gap:1rem}.company h2{margin:0;font-size:1.2rem}.company span{color:#637169;font-size:.85rem}.company>strong{color:#176b52}ul{display:grid;gap:.45rem;margin:0;padding:0;list-style:none}li{display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:.75rem;border-radius:.7rem;background:#f6f8f7}li>div{display:grid;gap:.15rem}li>div span{color:#637169;font-size:.8rem}.status{padding:.3rem .55rem;border-radius:999px;color:#68521b;background:#fff1c9;font-size:.72rem;font-weight:800}.status.active{color:#176b52;background:#e8f4ed}@media(max-width:40rem){.company,li{align-items:start;flex-direction:column}}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InvitationsReferralsAdminComponent {
  private readonly discovery = inject(SupplierDiscoveryService);
  readonly candidates = signal<SupplierCandidate[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly candidatesWithInvitations = computed(() => this.candidates().filter(candidate => candidate.invitations.length > 0));
  readonly invitationCount = computed(() => this.candidatesWithInvitations().reduce((total, candidate) => total + candidate.invitations.length, 0));

  constructor() { this.load(); }
  load(): void {
    this.loading.set(true);this.error.set('');
    this.discovery.candidates().subscribe({
      next: values => { this.candidates.set(values); this.loading.set(false); },
      error: () => { this.error.set('Invitațiile nu au putut fi încărcate.'); this.loading.set(false); }
    });
  }

  statusLabel(status: SupplierInvitationStatus): string {
    return ({ Active: 'Activă', Claimed: 'Acceptată', Revoked: 'Revocată', Expired: 'Expirată' })[status];
  }
}
