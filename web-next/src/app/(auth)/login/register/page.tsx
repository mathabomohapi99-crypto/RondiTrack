import { ClearPinsOnLogin } from '@/components/ClearPinsOnLogin';
import { RegisterForm } from './RegisterForm';

// No request data is read here, so this page is fully static; the form is a Client Component.
export default function RegisterPage() {
  return (
    <main className="mx-auto mt-24 max-w-sm p-4">
      <ClearPinsOnLogin />
      <h1 className="text-2xl font-bold">Create your RondiTrack account</h1>
      <RegisterForm />
    </main>
  );
}