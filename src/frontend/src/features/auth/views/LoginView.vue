<script setup lang="ts">
import { ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useForm } from 'vee-validate';
import { toTypedSchema } from '@vee-validate/zod';
import { z } from 'zod';
import InputText from 'primevue/inputtext';
import Password from 'primevue/password';
import Button from 'primevue/button';
import Message from 'primevue/message';
import ProgressSpinner from 'primevue/progressspinner';

import { useAuthStore } from '@/app/stores/auth.store';
import { ApiError } from '@/shared/api/errors';
import { getSafeRedirect } from '@/shared/utils/security';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

const errorMessage = ref<string>('');

// Zod validation schema
const loginSchema = toTypedSchema(
  z.object({
    username: z
      .string({ required_error: 'Username is required' })
      .trim()
      .min(1, 'Username is required'),
    password: z
      .string({ required_error: 'Password is required' })
      .min(1, 'Password is required'),
  })
);

const { handleSubmit, defineField, errors, isSubmitting } = useForm({
  validationSchema: loginSchema,
  initialValues: {
    username: '',
    password: '',
  },
});

const [username, usernameAttrs] = defineField('username');
const [password, passwordAttrs] = defineField('password');

const onSubmit = handleSubmit(async (values) => {
  errorMessage.value = '';

  try {
    await authStore.login(values.username.trim(), values.password);
    const target = getSafeRedirect(route.query.redirect, '/');
    await router.push(target);
  } catch (err: unknown) {
    if (err instanceof ApiError) {
      if (err.status === 401) {
        errorMessage.value = err.detail || 'Invalid username or password.';
      } else {
        errorMessage.value = 'Unable to connect to server. Please try again later.';
      }
    } else {
      errorMessage.value = 'Unable to connect to server. Please try again later.';
    }
  }
});
</script>

<template>
  <div class="min-h-screen bg-slate-900 flex items-center justify-center p-4">
    <div class="w-full max-w-sm bg-white rounded-xl shadow-lg border border-slate-200 p-6 sm:p-8">
      <!-- System Title & Branding -->
      <div class="mb-6 text-center">
        <div class="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-slate-900 text-white shadow-xs mb-3">
          <i class="pi pi-shield text-xl"></i>
        </div>
        <h1 class="text-2xl font-bold tracking-tight text-slate-900">PrIMA</h1>
        <p class="text-xs text-slate-500 mt-1">Proactive Inspection Monitoring App</p>
        <div class="mt-2.5 inline-block px-2.5 py-0.5 rounded-full bg-slate-100 text-slate-700 text-[11px] font-semibold tracking-wide uppercase">
          EDP / IT Support Sign In
        </div>
      </div>

      <!-- Inline Credential / Unexpected Error Banner -->
      <Message
        v-if="errorMessage"
        severity="error"
        :closable="false"
        class="mb-4 text-xs"
        role="alert"
      >
        {{ errorMessage }}
      </Message>

      <!-- Sign In Form -->
      <form @submit="onSubmit" novalidate class="space-y-4">
        <!-- Username Field -->
        <div>
          <label for="username" class="block text-xs font-semibold text-slate-700 mb-1.5">
            Username
          </label>
          <InputText
            id="username"
            v-model="username"
            v-bind="usernameAttrs"
            type="text"
            autocomplete="username"
            placeholder="e.g. ahmad.budi"
            :invalid="Boolean(errors.username)"
            class="w-full text-sm"
          />
          <small v-if="errors.username" class="text-rose-600 text-xs mt-1 block font-medium" role="alert">
            {{ errors.username }}
          </small>
        </div>

        <!-- Password Field with Toggle Visibility -->
        <div>
          <label for="password" class="block text-xs font-semibold text-slate-700 mb-1.5">
            Password
          </label>
          <Password
            id="password"
            v-model="password"
            v-bind="passwordAttrs"
            :feedback="false"
            toggleMask
            autocomplete="current-password"
            placeholder="••••••••"
            :invalid="Boolean(errors.password)"
            fluid
            class="w-full"
            :inputClass="'w-full text-sm'"
          />
          <small v-if="errors.password" class="text-rose-600 text-xs mt-1 block font-medium" role="alert">
            {{ errors.password }}
          </small>
        </div>

        <!-- Sign In Button with Progress Spinner -->
        <div class="pt-2">
          <Button
            type="submit"
            :disabled="isSubmitting"
            class="w-full py-2.5 px-4 bg-slate-900 hover:bg-slate-800 text-white font-medium rounded-lg transition-colors flex items-center justify-center gap-2 text-sm border-0 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
          >
            <ProgressSpinner
              v-if="isSubmitting"
              style="width: 18px; height: 18px;"
              class="[&_circle]:!stroke-white"
              strokeWidth="4"
              animationDuration=".6s"
              aria-label="Signing in progress"
            />
            <span>{{ isSubmitting ? 'Signing In...' : 'Sign In' }}</span>
          </Button>
        </div>
      </form>
    </div>
  </div>
</template>
