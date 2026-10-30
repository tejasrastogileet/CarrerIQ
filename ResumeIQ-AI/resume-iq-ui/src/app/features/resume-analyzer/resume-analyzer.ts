import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';

import { ResumeAnalysis } from '../../core/models/resume-analysis.model';
import { ResumeAnalysisRequest } from '../../core/models/resume-analysis-request.model';
import { ResumeService } from '../../core/services/resume';

@Component({
  selector: 'app-resume-analyzer',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './resume-analyzer.html',
  styleUrl: './resume-analyzer.scss'
})
export class ResumeAnalyzer {
  private readonly resumeService = inject(ResumeService);
  private validationAttempt = 0;

  readonly maxFileSizeBytes = 10 * 1024 * 1024;
  readonly acceptedFileTypes = '.pdf,.doc,.docx,.csv';

  resumeText = '';
  analysis = signal<ResumeAnalysis | null>(null);
  isLoading = signal(false);
  errorMessage = signal('');
  selectedFile = signal<File | null>(null);
  isValidatingFile = signal(false);
  isDragging = signal(false);
  mobileMenuOpen = signal(false);

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(isOpen => !isOpen);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  onFileInputChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0) ?? null;
    input.value = '';
    void this.selectResumeFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onFileDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    void this.selectResumeFile(event.dataTransfer?.files.item(0) ?? null);
  }

  async selectResumeFile(file: File | null): Promise<void> {
    if (!file) {
      return;
    }

    const attempt = ++this.validationAttempt;
    const extension = file.name.split('.').pop()?.toLowerCase() ?? '';

    this.selectedFile.set(null);
    this.resumeText = '';
    this.analysis.set(null);
    this.errorMessage.set('');
    this.isValidatingFile.set(false);

    if (!['pdf', 'doc', 'docx', 'csv'].includes(extension)) {
      this.errorMessage.set('Unsupported file format. Choose a PDF, DOCX, DOC, or CSV file.');
      return;
    }

    if (file.size === 0) {
      this.errorMessage.set('This file is empty. Choose a resume file that contains content.');
      return;
    }

    if (file.size > this.maxFileSizeBytes) {
      this.errorMessage.set('This file exceeds the 10 MB limit. Choose a smaller file.');
      return;
    }

    this.isValidatingFile.set(true);
    try {
      const message = await this.validateFileContent(file, extension);
      if (attempt !== this.validationAttempt) {
        return;
      }

      this.isValidatingFile.set(false);
      if (message) {
        this.errorMessage.set(message);
        return;
      }

      this.selectedFile.set(file);
    } catch {
      if (attempt !== this.validationAttempt) {
        return;
      }

      this.isValidatingFile.set(false);
      this.errorMessage.set('The file could not be read. Choose a valid PDF, DOCX, DOC, or CSV file.');
    }
  }

  clearSelectedFile(input?: HTMLInputElement): void {
    this.validationAttempt++;
    this.selectedFile.set(null);
    this.isValidatingFile.set(false);
    this.errorMessage.set('');
    if (input) {
      input.value = '';
    }
  }

  formatFileSize(size: number): string {
    return size < 1024 * 1024
      ? `${(size / 1024).toFixed(0)} KB`
      : `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  onResumeTextChanged(input: HTMLInputElement): void {
    if (this.resumeText.trim()) {
      if (this.selectedFile() || this.isValidatingFile()) {
        this.clearSelectedFile(input);
      } else {
        this.errorMessage.set('');
      }
    }
  }

  analyzeResume(): void {
    if (this.isLoading() || this.isValidatingFile()) {
      return;
    }

    const file = this.selectedFile();
    if (file) {
      this.submitAnalysis(this.resumeService.analyzeResumeFile(file));
      return;
    }

    if (!this.resumeText.trim()) {
      this.errorMessage.set('Choose a resume file or paste resume text to continue.');
      return;
    }

    const request: ResumeAnalysisRequest = { resumeText: this.resumeText };
    this.submitAnalysis(this.resumeService.analyzeResume(request));
  }

  resetAnalysis(): void {
    this.validationAttempt++;
    this.analysis.set(null);
    this.errorMessage.set('');
    this.selectedFile.set(null);
    this.isValidatingFile.set(false);
    this.isDragging.set(false);
    this.resumeText = '';
  }

  private async validateFileContent(file: File, extension: string): Promise<string | null> {
    if (extension === 'csv') {
      return this.validateCsvContent(file);
    }

    const bytes = new Uint8Array(await file.slice(0, 1024).arrayBuffer());
    const validContent = extension === 'pdf'
      ? this.containsAsciiSignature(bytes, '%PDF-')
      : extension === 'doc'
        ? this.startsWithBytes(bytes, [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1])
        : this.startsWithBytes(bytes, [0x50, 0x4b]) && bytes.length >= 4;

    return validContent
      ? null
      : `The file content does not match a valid ${extension.toUpperCase()} document.`;
  }

  private async validateCsvContent(file: File): Promise<string | null> {
    const bytes = await file.arrayBuffer();
    let content: string;

    try {
      content = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    } catch {
      return 'The CSV file must contain readable UTF-8 text.';
    }

    const csv = content.replace(/^\uFEFF/, '');
    if (!csv.trim()) {
      return 'This CSV file is empty. Choose a file that contains resume content.';
    }

    if (/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/.test(csv)) {
      return 'The CSV file contains binary or unsupported content.';
    }

    let insideQuotes = false;
    for (let index = 0; index < csv.length; index++) {
      if (csv[index] !== '"') {
        continue;
      }

      if (insideQuotes && csv[index + 1] === '"') {
        index++;
      } else {
        insideQuotes = !insideQuotes;
      }
    }

    return insideQuotes ? 'The CSV file has an unclosed quoted field.' : null;
  }

  private containsAsciiSignature(bytes: Uint8Array, signature: string): boolean {
    const encoded = new TextEncoder().encode(signature);
    for (let start = 0; start <= bytes.length - encoded.length; start++) {
      if (encoded.every((byte, offset) => bytes[start + offset] === byte)) {
        return true;
      }
    }

    return false;
  }

  private startsWithBytes(bytes: Uint8Array, signature: number[]): boolean {
    return signature.every((byte, index) => bytes[index] === byte);
  }

  private submitAnalysis(request: Observable<ResumeAnalysis>): void {
    this.analysis.set(null);
    this.errorMessage.set('');
    this.isLoading.set(true);

    request
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: response => {
          this.analysis.set(response);
          setTimeout(() => {
            const resultsHeading = document.getElementById('results-heading');
            if (typeof resultsHeading?.scrollIntoView === 'function') {
              resultsHeading.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
          });
        },
        error: error => this.errorMessage.set(this.getErrorMessage(error))
      });
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const apiMessage = typeof error.error?.message === 'string'
        ? error.error.message
        : null;

      if (apiMessage) {
        return apiMessage;
      }

      if (error.status === 0) {
        return 'CareerIQ could not reach the analysis service. Check that the backend is running and try again.';
      }
      if (error.status === 413) {
        return 'This file exceeds the 10 MB limit. Choose a smaller file.';
      }
      if (error.status === 415) {
        return 'This file format is not supported. Choose a PDF, DOCX, DOC, or CSV file.';
      }
      if (error.status >= 500) {
        return 'The analysis service could not complete this request. Please try again shortly.';
      }
    }

    return 'Unable to analyze this resume. Check the file and try again.';
  }
}
