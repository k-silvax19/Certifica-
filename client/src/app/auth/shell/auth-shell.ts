import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideFileText } from '@lucide/angular';

@Component({
  imports: [LucideFileText, RouterLink],
  selector: 'app-auth-shell',
  templateUrl: './auth-shell.html',
})
export class AuthShell {}
