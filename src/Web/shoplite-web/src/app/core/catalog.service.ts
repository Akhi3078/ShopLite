import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { PagedResult, Product, ProductDetail, ProductInput } from './models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.gatewayUrl}/catalog/products`;

  list(category?: string, page = 1, pageSize = 20) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);

    if (category) {
      params = params.set('category', category);
    }

    return this.http.get<PagedResult<Product>>(this.base, { params });
  }

  get(id: string) {
    return this.http.get<ProductDetail>(`${this.base}/${id}`);
  }

  create(product: ProductInput) {
    return this.http.post<{ id: string }>(this.base, product);
  }

  update(id: string, product: ProductInput) {
    return this.http.put<void>(`${this.base}/${id}`, product);
  }

  remove(id: string) {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
