import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, Routes } from '@angular/router';
import { CHAVE_ARMAZENAMENTO, URL_API } from './auth/auth.tokens';
import { environment } from '../environments/environment.development';
import { authInterceptor } from './auth/auth.interceptor';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then((c) => c.Home),
  },
  {
    path: 'cadastro',
    loadComponent: () => import('./auth/cadastro/cadastro').then((c) => c.Cadastro),
  },
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login').then((l) => l.Login),
  },
  {
    path: 'cursos',
    loadComponent: () => import('./cursos/shell/cursos-shell').then((l) => l.CursosShell),
    children: [
      { path: '', redirectTo: 'listar', pathMatch: 'full' },
      {
        path: 'listar',
        loadComponent: () => import('./cursos/listar/listar-cursos').then((l) => l.ListarCursos),
      },
      {
        path: 'detalhes/:cursoId',
        loadComponent: () =>
          import('./cursos/detalhes/detalhes-curso').then((c) => c.DetalhesCurso),
      },
      {
        path: 'cadastrar',
        loadComponent: () =>
          import('./cursos/cadastrar/cadastrar-cursos').then((l) => l.CadastrarCurso),
      },
    ],
  },
  {
    path: '**',
    redirectTo: '',
  },
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: URL_API,
      useValue: environment.urlApi,
    },
    {
      provide: CHAVE_ARMAZENAMENTO,
      useValue: 'certica+.sessao',
    },
  ],
};
