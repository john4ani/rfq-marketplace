import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { CategoryOption, ProjectTemplateOption, SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';

@Component({selector:'app-category-catalog',imports:[ReactiveFormsModule],templateUrl:'./category-catalog.component.html',styleUrl:'./category-catalog.component.scss',changeDetection:ChangeDetectionStrategy.OnPush})
export class CategoryCatalogComponent {
  private readonly service=inject(SupplierSettingsService);
  readonly categories=signal<CategoryOption[]>([]);readonly templates=signal<ProjectTemplateOption[]>([]);
  readonly selectedTemplateCategories=signal<string[]>([]);readonly similar=signal<CategoryOption[]>([]);
  readonly loading=signal(true);readonly saving=signal(false);readonly message=signal('');readonly error=signal('');
  private pendingCategoryName='';
  readonly categoryName=new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(2),Validators.maxLength(120)]});
  readonly templateForm=new FormGroup({name:new FormControl('',{nonNullable:true,validators:[Validators.required,Validators.minLength(2),Validators.maxLength(180)]}),description:new FormControl('',{nonNullable:true,validators:Validators.maxLength(2000)})});
  constructor(){this.load();}
  load():void{this.loading.set(true);this.error.set('');this.categories.set([]);this.templates.set([]);forkJoin({categories:this.service.getCategories(),templates:this.service.getProjectTemplates()}).subscribe({next:r=>{this.categories.set(r.categories);this.templates.set(r.templates);this.loading.set(false);},error:e=>{this.loading.set(false);this.error.set(this.apiError(e,'Catalogul nu a putut fi încărcat.'));}});}
  selectableCategories():CategoryOption[]{const parents=new Set(this.categories().flatMap(c=>c.parentId?[c.parentId]:[]));return this.categories().filter(c=>!parents.has(c.id));}
  addCategory():void{if(this.categoryName.invalid||this.saving()){this.categoryName.markAsTouched();return;}const name=this.categoryName.value.trim();const confirm=this.pendingCategoryName===name&&this.similar().length>0;this.saving.set(true);this.error.set('');this.message.set('');this.service.addCategory(name,confirm).subscribe({next:c=>{if(!this.categories().some(v=>v.id===c.id))this.categories.update(v=>[...v,c]);this.saving.set(false);this.similar.set([]);this.pendingCategoryName='';this.categoryName.reset();this.message.set(c.alreadyExisted?`Categoria „${c.name}” exista deja.`:`Categoria „${c.name}” a fost adăugată în catalogul central.`);},error:(e:HttpErrorResponse)=>{this.saving.set(false);if(e.status===409&&Array.isArray(e.error?.similarCategories)){this.pendingCategoryName=name;this.similar.set(e.error.similarCategories);this.message.set('Verifică rezultatele similare. Confirmă numai dacă este o categorie distinctă.');return;}this.error.set(this.apiError(e,'Categoria nu a putut fi adăugată.'));}});}
  clearConfirmation():void{this.pendingCategoryName='';this.similar.set([]);}
  toggleTemplateCategory(id:string):void{const v=this.selectedTemplateCategories();this.selectedTemplateCategories.set(v.includes(id)?v.filter(x=>x!==id):[...v,id]);}
  createTemplate():void{if(this.templateForm.invalid||!this.selectedTemplateCategories().length||this.saving()){this.templateForm.markAllAsTouched();this.error.set('Completează denumirea și selectează cel puțin o categorie.');return;}const v=this.templateForm.getRawValue();this.saving.set(true);this.error.set('');this.message.set('');this.service.addProjectTemplate(v.name.trim(),v.description.trim()||null,this.selectedTemplateCategories()).subscribe({next:t=>{this.templates.update(x=>[...x,t]);this.templateForm.reset();this.selectedTemplateCategories.set([]);this.saving.set(false);this.message.set(`Șablonul „${t.name}” este disponibil la crearea unei cereri.`);},error:e=>{this.saving.set(false);this.error.set(this.apiError(e,'Șablonul nu a putut fi creat.'));}});}
  categoryNames(ids:string[]):string{return ids.map(id=>this.categories().find(c=>c.id===id)?.name??'Categorie indisponibilă').join(', ');}
  private apiError(e:HttpErrorResponse,fallback:string):string{const messages=e.error?.errors?Object.values(e.error.errors as Record<string,string[]>).flat():[];return messages.length?messages.join(' '):e.error?.detail??fallback;}
}
