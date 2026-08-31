import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Terms of Service (ToS). The platform is positioned as an assisted-drafting software tool (Copilot), not a
 * provider of legal or technical consulting; the Limitation of Liability clause below is the explicit ToS anchor
 * referenced by the download disclaimer (checkbox) in the SEAP flow.
 */
@Component({
  selector: 'app-terms',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block;max-width:52rem;margin:0 auto;padding:2rem 1.2rem 3rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}h1{font-size:clamp(1.8rem,4vw,2.4rem);letter-spacing:-.04em;margin:.4rem 0 1rem}p{line-height:1.65}.muted{color:var(--muted)}h2{font-size:1.15rem;margin:1.8rem 0 .4rem}`],
  template: `<section>
    <a class="back-link" routerLink="/">← Înapoi</a>
    <h1>Termeni și Condiții</h1>
    <p class="muted">Ultima actualizare: august 2026</p>

    <h2>1. Serviciul furnizat</h2>
    <p>CompaSpot furnizează un <strong>instrument software de redactare asistată</strong> („Copilot") care generează
      propuneri tehnice și liste de cantități pe baza documentelor și informațiilor furnizate de tine. CompaSpot
      <strong>nu furnizează servicii de consultanță juridică sau tehnică</strong> și nu înlocuiește specialiști
      autorizați (consultanți în achiziții publice, ingineri, juriști).</p>

    <h2>2. Caracterul orientativ al documentelor generate</h2>
    <p>Documentele generate prin intermediul platformei au caracter orientativ și sunt destinate să sprijine redactarea
      unei oferte. Ele nu constituie o opinie juridică sau tehnică și nu garantează admisibilitatea, conformitatea sau
      acceptarea ofertei de către autoritatea contractantă.</p>

    <h2>3. Limitarea răspunderii (Limitation of Liability)</h2>
    <p>În limita maximă permisă de lege, CompaSpot, afiliații și furnizorii săi nu sunt răspunzători pentru pierderi
      directe, indirecte, accidentale, speciale, punitive sau consecutive, inclusiv, fără limitare, pentru pierderea
      de oportunități, pierderea de profit, pierderea de date sau orice altă daună, care decurg din sau în legătură cu
      utilizarea platformei, a documentelor generate sau a serviciilor conexe — chiar și atunci când CompaSpot a fost
      informată despre posibilitatea producerii acestor pierderi.</p>

    <h2>4. Responsabilitatea utilizatorului</h2>
    <p>Prin utilizarea platformei și prin descărcarea oricărui document generat, ești de acord că
      <strong>răspunderea exclusivă pentru verificarea, completarea, corectitudinea și asumarea conținutului</strong>
      documentelor îți aparține, înainte de semnarea electronică și depunerea acestora în SEAP sau în orice alt sistem
      de achiziții publice.</p>

    <h2>5. Date și confidențialitate</h2>
    <p>Documentele încărcate în Depozitul de Documente sunt utilizate exclusiv pentru generarea ofertelor solicitate și sunt
      protejate conform politicii de confidențialitate. Nu vindem și nu partajăm datele firmei tale cu terți în scopuri
      comerciale.</p>

    <h2>6. Taxare</h2>
    <p>Generarea de documente .docx și .xlsx este taxată conform modelului de prețuri afișat înainte de generare, pe
      baza valorii estimate a achiziției. Prima generare este gratuită. Plățile sunt procesate de un procesator de plăți
      autorizat (Stripe).</p>

    <h2>7. Legea aplicabilă</h2>
    <p>Prezentul contract este guvernat de legislația română. Orice litigiu este de competența instanțelor din România.</p>
  </section>`
})
export class TermsComponent {}
