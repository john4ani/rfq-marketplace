import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { RegisterComponent } from './register.component';

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let component: RegisterComponent;
  let response: Subject<void>;
  let registerCalls: number;

  beforeEach(async () => {
    response = new Subject<void>();
    registerCalls = 0;
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            register: () => {
              registerCalls += 1;
              return response.asObservable();
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    component.form.setValue({ email: 'new@example.test', password: 'password123' });
    fixture.detectChanges();
  });

  it('submits only once while registration is in progress', () => {
    component.submit();
    component.submit();
    fixture.detectChanges();

    expect(registerCalls).toBe(1);
    expect(component.submitting()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Se creează contul…');
  });

  it('prevents resubmission and presents sign-in after success', () => {
    component.submit();
    response.next();
    response.complete();
    fixture.detectChanges();
    component.submit();

    expect(registerCalls).toBe(1);
    expect(component.registrationSucceeded()).toBe(true);
    expect(component.form.controls.password.value).toBe('');
    expect(fixture.nativeElement.textContent).toContain('Contul a fost creat. Autentificați-vă pentru a continua.');
    expect(fixture.nativeElement.querySelector('a.button-link')?.getAttribute('href')).toBe('/sign-in');
  });
});
