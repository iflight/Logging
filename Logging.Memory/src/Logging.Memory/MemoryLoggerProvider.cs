﻿namespace iflight.Logging
{
    using System;
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class MemoryLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, MemoryLogger> _loggers = new ConcurrentDictionary<string, MemoryLogger>();

        private readonly Func<string, LogLevel, bool> _filter;

        private IMemoryLoggerSettings _settings;

        public MemoryLoggerProvider(Func<string, LogLevel, bool> filter, int maxLogCount = 200)
        {
            _filter = filter;
            _settings = new MemoryLoggerSettings()
            {
                MaxLogCount = maxLogCount
            };
        }

        public MemoryLoggerProvider(IMemoryLoggerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _settings = settings;

            if (_settings.ChangeToken != null)
            {
                _settings.ChangeToken.RegisterChangeCallback(OnConfigurationReload, null);
            }
        }

        private void OnConfigurationReload(object state)
        {
            // The settings object needs to change here, because the old one is probably holding on 
            // to an old change token. 
            _settings = _settings.Reload();

            foreach (var logger in _loggers.Values)
            {
                logger.Filter = GetFilter(logger.Name, _settings);
            }

            // The token will change each time it reloads, so we need to register again. 
            if (_settings?.ChangeToken != null)
            {
                _settings.ChangeToken.RegisterChangeCallback(OnConfigurationReload, null);
            }
        }

        public ILogger CreateLogger(string name)
        {
            return _loggers.GetOrAdd(name, CreateLoggerImplementation);
        }

        private MemoryLogger CreateLoggerImplementation(string name)
        {
            return new MemoryLogger(name, GetFilter(name, _settings), _settings.MaxLogCount);
        }

        private Func<string, LogLevel, bool> GetFilter(string name, IMemoryLoggerSettings settings)
        {
            if (_filter != null)
            {
                return _filter;
            }

            if (settings != null)
            {
                foreach (var prefix in GetKeyPrefixes(name))
                {
                    LogLevel level;
                    if (settings.TryGetSwitch(prefix, out level))
                    {
                        return (n, l) => l >= level;
                    }
                }
            }

            return (n, l) => false;
        }

        private IEnumerable<string> GetKeyPrefixes(string name)
        {
            while (!string.IsNullOrEmpty(name))
            {
                yield return name;
                var lastIndexOfDot = name.LastIndexOf('.');
                if (lastIndexOfDot == -1)
                {
                    yield return "Default";
                    break;
                }
                name = name.Substring(0, lastIndexOfDot);
            }
        }



        public void Dispose()
        {
        }
    }
}
