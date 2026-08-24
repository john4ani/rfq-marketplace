import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProfileService, RepresentedCompany } from '../../core/profiles/profile.service';
import { LanguageService } from '../../core/i18n/language.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, FormsModule],
  template: `<section class="need-starter" aria-labelledby="need-starter-title">
      <header><h1 id="need-starter-title">{{ i18n.t('De ce ai nevoie?') }}</h1>
        <p>{{ i18n.t('Spune ce ai nevoie. Primește oferte relevante din zona ta.') }}</p></header>
      <section class="general-entry" aria-labelledby="general-entry-title"><div><p class="eyebrow">{{ i18n.t('Începe simplu') }}</p><h2 id="general-entry-title">{{ i18n.t('Caut ceva') }}</h2><p>{{ i18n.t('Produs, material, piesă, serviciu sau echipament.') }}</p></div>
        <label class="sr-only" for="dashboard-need">{{ i18n.t('Descrie pe scurt ce ai nevoie') }}</label><textarea id="dashboard-need" [(ngModel)]="needText" rows="3" [placeholder]="i18n.t('Ex: Am nevoie de un alternator pentru VW Passat 2018, în Suceava.')"></textarea>
        <button type="button" (click)="startNeed()" [disabled]="needText.trim().length < 10">{{ i18n.t('Continuă') }}</button>
        <small>{{ i18n.t('Te ajutăm să clarifici detaliile. Nimic nu este trimis fără confirmarea ta.') }}</small>
        <a class="manual-entry" routerLink="/rfqs/new/advanced">{{ i18n.t('Am deja o listă / prefer să completez manual') }}</a>
      </section>
      <section class="construction-entry"><div><h2>{{ i18n.t('Am un proiect de construcții') }}</h2><p>{{ i18n.t('Te ajutăm să structurezi proiectul și achizițiile necesare.') }}</p></div><a routerLink="/rfqs/new/construction">{{ i18n.t('Începe proiectul') }}</a></section>
    </section>

    @if(companyLoading()) {
      <section class="supplier-intro" role="status"><div><p class="eyebrow">{{ i18n.t('Firma ta') }}</p><h2>{{ i18n.t('Încărcăm contextul firmei…') }}</h2></div></section>
    } @else if(companyLoadError()) {
      <section class="supplier-intro" role="alert"><div><p class="eyebrow">{{ i18n.t('Firma ta') }}</p><h2>{{ i18n.t('Contextul firmei nu este disponibil momentan.') }}</h2><p>{{ i18n.t('Poți continua să cumperi și poți reîncerca din Cont.') }}</p></div><a routerLink="/account">{{ i18n.t('Deschide contul') }} <span aria-hidden="true">→</span></a></section>
    } @else if(companies().length === 1) {
      <section class="supplier-intro">
        <div><p class="eyebrow">{{ i18n.t('Firma ta') }}</p><h2>{{ i18n.t('Oportunități pentru') }} {{ companies()[0].name }}</h2><p>{{ i18n.t('Vezi cererile potrivite și urmărește ofertele trimise de firmă.') }}</p></div>
        <div class="supplier-links"><a [routerLink]="'/opportunities'" [queryParams]="{ companyId: companies()[0].id }">{{ i18n.t('Vezi oportunitățile') }} <span aria-hidden="true">→</span></a>
          <a class="secondary-action" [routerLink]="'/company/offers'" [queryParams]="{ companyId: companies()[0].id }">{{ i18n.t('Ofertele firmei') }}</a></div>
      </section>
    } @else if (companies().length > 1) {
      <section class="supplier-intro"><div><p class="eyebrow">{{ i18n.t('Firmele tale') }}</p><h2>{{ i18n.t('Alege firma pentru oportunități') }}</h2><p>{{ i18n.t('Contextul firmei se alege doar când ai nevoie de el.') }}</p></div><a routerLink="/account">{{ i18n.t('Alege firma') }} <span aria-hidden="true">→</span></a></section>
    } @else {
      <section class="supplier-intro"><div><p class="eyebrow">{{ i18n.t('Vrei să vinzi prin CompaSpot?') }}</p><h2>{{ i18n.t('Adaugă firma') }}</h2><p>{{ i18n.t('Înregistrează datele esențiale, ce oferă firma și zona în care lucrează. Contul tău personal rămâne disponibil pentru achiziții.') }}</p></div><a routerLink="/account/company/new">{{ i18n.t('Adaugă firma') }} <span aria-hidden="true">→</span></a></section>
    }`,
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardComponent {
  private readonly router = inject(Router);
  private readonly profiles = inject(ProfileService);
  readonly i18n = inject(LanguageService);
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
