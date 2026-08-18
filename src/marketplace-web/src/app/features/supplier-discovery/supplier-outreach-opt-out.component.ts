import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { SupplierDiscoveryService } from '../../core/supplier-discovery/supplier-discovery.service';

@Component({
  selector: 'app-supplier-outreach-opt-out',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`.page{max-width:38rem;margin:4rem auto;padding:1.5rem}.card{padding:1.5rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface)}`],
  template: `<main class="page"><section class="card"><p class="eyebrow">Preferințe contact</p><h1>{{title()}}</h1><p>{{message()}}</p></section></main>`
})
export class SupplierOutreachOptOutComponent {
  private readonly api = inject(SupplierDiscoveryService); private readonly route = inject(ActivatedRoute);
  readonly title = signal('Se actualizează preferințele…'); readonly message = signal('Te rugăm să aștepți un moment.');
  constructor() { const token = this.route.snapshot.paramMap.get('token'); if (!token) { this.title.set('Link indisponibil'); this.message.set('Linkul de dezabonare nu este valid.'); return; } this.api.optOut(token).subscribe({ next: () => { this.title.set('Nu vei mai primi e-mailuri de oportunitate'); this.message.set('Preferința a fost salvată. Dacă ai primit mesajul din greșeală, contactează support@compaspot.com.'); }, error: () => { this.title.set('Link indisponibil'); this.message.set('Linkul nu mai este valid sau a expirat.'); } }); }
}
