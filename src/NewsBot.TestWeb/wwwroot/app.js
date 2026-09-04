const apiUrlInput = document.getElementById('apiUrl');
const userIdInput = document.getElementById('userId');
const commandText = document.getElementById('commandText');
const commandList = document.getElementById('commandList');
const output = document.getElementById('output');
const sendBtn = document.getElementById('sendBtn');
const clearBtn = document.getElementById('clearBtn');

const savedApiUrl = localStorage.getItem('newsbot.test.apiUrl');
if (savedApiUrl) apiUrlInput.value = savedApiUrl;

function getApiUrl() {
  const url = apiUrlInput.value.trim().replace(/\/$/, '');
  localStorage.setItem('newsbot.test.apiUrl', url);
  return url;
}

function renderCommands(commands) {
  commandList.innerHTML = '';
  for (const cmd of commands) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'command-chip';
    button.title = cmd.description;
    button.textContent = cmd.name;
    button.addEventListener('click', () => {
      commandText.value = cmd.example;
      commandText.focus();
    });
    commandList.appendChild(button);
  }
}

function formatResult(result) {
  if (!result.responses?.length) {
    return 'Бот не вернул сообщений.';
  }

  return result.responses.map((response, index) => {
    const parts = [`--- Ответ ${index + 1} ---`];
    if (response.body) parts.push(response.body);

    if (response.attachments?.length) {
      for (const file of response.attachments) {
        parts.push(`📎 ${file.name} (${file.mediaType}, ${file.sizeBytes} bytes)`);
      }
    }

    if (response.optionsHint) {
      parts.push(`[keyboard/options] ${response.optionsHint}`);
    }

    return parts.join('\n');
  }).join('\n\n');
}

async function loadCommands() {
  try {
    const response = await fetch(`${getApiUrl()}/api/commands`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const commands = await response.json();
    renderCommands(commands);
  } catch (error) {
    commandList.innerHTML = `<span class="error">Не удалось загрузить команды: ${error.message}</span>`;
  }
}

async function sendCommand() {
  const userId = Number(userIdInput.value);
  const text = commandText.value.trim();

  if (!userId || userId <= 0) {
    output.textContent = 'Укажите корректный User ID.';
    return;
  }

  if (!text) {
    output.textContent = 'Введите команду.';
    return;
  }

  output.textContent = 'Выполняется...';
  sendBtn.disabled = true;

  try {
    const response = await fetch(`${getApiUrl()}/api/commands/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId, text })
    });

    const payload = await response.json();
    if (!response.ok) {
      output.textContent = payload.title || payload || `HTTP ${response.status}`;
      return;
    }

    output.textContent = formatResult(payload);
  } catch (error) {
    output.textContent = `Ошибка: ${error.message}`;
  } finally {
    sendBtn.disabled = false;
  }
}

sendBtn.addEventListener('click', sendCommand);
clearBtn.addEventListener('click', () => { output.textContent = ''; });
commandText.addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault();
    sendCommand();
  }
});

loadCommands();
