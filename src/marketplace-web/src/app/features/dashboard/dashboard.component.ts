import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProfileService, RepresentedCompany } from '../../core/profiles/profile.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, FormsModule],
  template: `<section class="need-starter" aria-labelledby="need-starter-title">
      <header><h1 id="need-starter-title">De ce ai nevoie?</h1>
        <p>Descrie ce cauți. Te ajutăm să găsești furnizori relevanți și să compari ofertele.</p></header>
      <section class="general-entry" aria-labelledby="general-entry-title"><div><p class="eyebrow">Începe simplu</p><h2 id="general-entry-title">Caut ceva</h2><p>Produs, material, piesă, serviciu sau echipament.</p></div>
        <label class="sr-only" for="dashboard-need">Descrie pe scurt ce ai nevoie</label><textarea id="dashboard-need" [(ngModel)]="needText" rows="3" placeholder="Ex: Am nevoie de un alternator pentru VW Passat 2018, în Suceava."></textarea>
        <button type="button" (click)="startNeed()" [disabled]="needText.trim().length < 10">Continuă</button>
        <small>Te ajutăm să clarifici detaliile. Nimic nu este trimis fără confirmarea ta.</small>
        <a class="manual-entry" routerLink="/rfqs/new/advanced">Am deja o listă / prefer să completez manual</a>
      </section>
      <section class="construction-entry"><div><h2>Am un proiect de construcții</h2><p>Te ajutăm să structurezi proiectul și achizițiile necesare.</p></div><a routerLink="/rfqs/new/construction">Începe proiectul</a></section>
    </section>

    @if(companyLoading()) {
      <section class="supplier-intro" role="status"><div><p class="eyebrow">Firma ta</p><h2>Încărcăm contextul firmei…</h2></div></section>
    } @else if(companyLoadError()) {
      <section class="supplier-intro" role="alert"><div><p class="eyebrow">Firma ta</p><h2>Contextul firmei nu este disponibil momentan.</h2><p>Poți continua să cumperi și poți reîncerca din Cont.</p></div><a routerLink="/account">Deschide contul <span aria-hidden="true">→</span></a></section>
    } @else if(companies().length === 1) {
      <section class="supplier-intro">
        <div><p class="eyebrow">Firma ta</p><h2>Oportunități pentru {{ companies()[0].name }}</h2><p>Vezi cererile potrivite și urmărește ofertele trimise de firmă.</p></div>
        <div class="supplier-links"><a [routerLink]="'/opportunities'" [queryParams]="{ companyId: companies()[0].id }">Vezi oportunitățile <span aria-hidden="true">→</span></a>
          <a class="secondary-action" [routerLink]="'/company/offers'" [queryParams]="{ companyId: companies()[0].id }">Ofertele firmei</a></div>
      </section>
    } @else if (companies().length > 1) {
      <section class="supplier-intro"><div><p class="eyebrow">Firmele tale</p><h2>Alege firma pentru oportunități</h2><p>Contextul firmei se alege doar când ai nevoie de el.</p></div><a routerLink="/account">Alege firma <span aria-hidden="true">→</span></a></section>
    } @else {
      <section class="supplier-intro"><div><p class="eyebrow">Vrei să vinzi prin CompaSpot?</p><h2>Adaugă firma</h2><p>Înregistrează datele esențiale, ce oferă firma și zona în care lucrează. Contul tău personal rămâne disponibil pentru achiziții.</p></div><a routerLink="/account/company/new">Adaugă firma <span aria-hidden="true">→</span></a></section>
    }`,
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  private readonly router = inject(Router);
  private readonly profiles = inject(ProfileService);
  readonly companies = signal<RepresentedCompany[]>([]);
  readonly companyLoading = signal(true);
  readonly companyLoadError = signal(false);
  needText = '';

  constructor() {
    this.profiles.getMyCompanies().subscribe({
      next: companies => { this.companies.set(companies); this.companyLoading.set(false); },
      error: () => { this.companyLoadError.set(true); this.companyLoading.set(false); }
    });
  }

  startNeed(): void {
    const description = this.needText.trim();
    void this.router.navigate(['/rfqs/new'], { queryParams: description ? { description } : undefined });
  }
}
