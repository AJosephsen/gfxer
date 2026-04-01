import './styles.css';

const app = document.querySelector<HTMLDivElement>('#app');

if (app) {
  app.innerHTML = `
    <main class="shell" aria-label="StrategyCards empty app shell">
      <h1>StrategyCards</h1>
      <p>Empty web app shell</p>
    </main>
  `;
}
