import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { ProductForm } from './product-form';

/**
 * Regression test for a real bug: opening a product for editing showed a blank form.
 *
 * The cause was reading the `:id` signal input inside the constructor. The router binds
 * route inputs *after* construction, so `this.id()` was `undefined`, the load was skipped,
 * and every field rendered empty. Loading in `ngOnInit` fixes it.
 *
 * The assertions deliberately read the rendered input elements rather than the form model.
 * The symptom was what the user saw on screen, so that is what is pinned here — a test
 * against `form.getRawValue()` would also have passed with a template that failed to bind.
 */
describe('ProductForm', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    httpMock = TestBed.inject(HttpTestingController);
  });

  it('fills every field from the product named by the :id input', () => {
    const fixture = TestBed.createComponent(ProductForm);

    // How the router supplies a route parameter with withComponentInputBinding(): the
    // input is set on an already-constructed component, which is the whole point.
    fixture.componentRef.setInput('id', '01a1a6ce-42e4-7065-8a61-7a3b7e91c174');
    fixture.detectChanges();

    const request = httpMock.expectOne(
      `${environment.gatewayUrl}/catalog/products/01a1a6ce-42e4-7065-8a61-7a3b7e91c174`,
    );
    expect(request.request.method).toBe('GET');

    request.flush({
      id: '01a1a6ce-42e4-7065-8a61-7a3b7e91c174',
      name: 'Standing Desk',
      description: 'Electric sit-stand desk.',
      price: 699,
      category: 'Furniture',
      createdAt: '2026-11-01T00:00:00Z',
      updatedAt: null,
    });

    fixture.detectChanges();

    const value = (selector: string) =>
      (fixture.nativeElement.querySelector(selector) as HTMLInputElement | null)?.value;

    expect(value('#name')).toBe('Standing Desk');
    expect(value('#description')).toBe('Electric sit-stand desk.');
    expect(value('#price')).toBe('699');
    expect(value('#category')).toBe('Furniture');
  });

  it('loads nothing in create mode, where there is no id', () => {
    const fixture = TestBed.createComponent(ProductForm);
    fixture.detectChanges();

    httpMock.expectNone(() => true);

    const name = fixture.nativeElement.querySelector('#name') as HTMLInputElement;
    expect(name.value).toBe('');
  });

  afterEach(() => {
    httpMock.verify();
  });
});
