import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { describeApiError } from '../../../core/api-error';
import { todayIso } from '../../../core/format';
import { MedicineService } from '../../../core/medicine.service';
import { SaveMedicineRequest } from '../../../core/models';

/** Money must have at most two decimal places - the brief is explicit about it. */
function twoDecimalPlaces(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === '' || value === undefined) {
    return null;
  }

  return /^\d+(\.\d{1,2})?$/.test(String(value)) ? null : { twoDecimals: true };
}

/** FR-03 - add a medicine. The same form edits one, because the fields are identical. */
@Component({
  selector: 'app-medicine-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './medicine-form.html',
  styleUrl: './medicine-form.css',
})
export class MedicineFormComponent implements OnInit {
  private readonly medicines = inject(MedicineService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  protected readonly today = todayIso();

  protected readonly medicineId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    brand: ['', [Validators.required, Validators.maxLength(120)]],
    expiryDate: ['', [Validators.required]],
    quantity: [0, [Validators.required, Validators.min(0), Validators.max(1_000_000)]],
    price: [0, [Validators.required, Validators.min(0), twoDecimalPlaces]],
    notes: ['', [Validators.maxLength(1000)]],
  });

  protected get isEdit(): boolean {
    return this.medicineId() !== null;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.medicineId.set(id);
    this.loading.set(true);

    this.medicines
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (medicine) => {
          this.form.patchValue({
            fullName: medicine.fullName,
            brand: medicine.brand,
            expiryDate: medicine.expiryDate,
            quantity: medicine.quantity,
            price: medicine.price,
            notes: medicine.notes ?? '',
          });
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(describeApiError(err, 'Could not load that medicine.'));
          this.loading.set(false);
        },
      });
  }

  protected submit(): void {
    this.error.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: SaveMedicineRequest = {
      fullName: value.fullName.trim(),
      brand: value.brand.trim(),
      expiryDate: value.expiryDate,
      quantity: Number(value.quantity),
      price: Number(value.price),
      notes: value.notes.trim() === '' ? null : value.notes.trim(),
    };

    this.saving.set(true);

    const id = this.medicineId();
    const request$ = id ? this.medicines.update(id, request) : this.medicines.create(request);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving.set(false);
        void this.router.navigate(['/medicines']);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(describeApiError(err, 'Could not save the medicine.'));
      },
    });
  }

  /** Returns the first message for a control, but only once the user has touched it. */
  protected errorFor(controlName: string): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || !(control.touched || control.dirty)) {
      return null;
    }

    const errors = control.errors ?? {};
    if (errors['required']) {
      return 'This field is required.';
    }
    if (errors['minlength']) {
      return 'Please enter at least 2 characters.';
    }
    if (errors['maxlength']) {
      return 'That is longer than the field allows.';
    }
    if (errors['min']) {
      return 'Value cannot be negative.';
    }
    if (errors['max']) {
      return 'That value is unrealistically large.';
    }
    if (errors['twoDecimals']) {
      return 'Price must have at most two decimal places.';
    }

    return 'That value is not valid.';
  }

  protected isInvalid(controlName: string): boolean {
    return this.errorFor(controlName) !== null;
  }
}
