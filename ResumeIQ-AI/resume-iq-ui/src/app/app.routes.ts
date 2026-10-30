import { Routes } from '@angular/router';
import { ResumeAnalyzer } from './features/resume-analyzer/resume-analyzer';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'analyze',
    pathMatch: 'full'
  },
  {
    path: 'analyze',
    component: ResumeAnalyzer
  },
  {
    path: '**',
    redirectTo: 'analyze'
  }
];