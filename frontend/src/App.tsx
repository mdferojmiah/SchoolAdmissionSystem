const apiUrl = import.meta.env.VITE_API_URL;

export default function App() {
  return (
    <main className="app">
      <h1>School Admission System</h1>
      <p>API: {apiUrl}</p>
    </main>
  );
}
