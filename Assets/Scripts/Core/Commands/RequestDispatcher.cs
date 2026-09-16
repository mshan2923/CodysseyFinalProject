using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

namespace Codyssey.Core.Commands
{
    /// <summary>
    /// Routes requests to one explicitly registered handler per request type.
    /// It does not decide whether a request is valid; the handler does.
    /// </summary>
    public sealed class RequestDispatcher
    {
        private readonly Dictionary<Type, Action<object>> handlers = new();

        public void Register<TRequest>(IRequestHandler<TRequest> handler)
            where TRequest : struct, IRequest
        {
            handlers[typeof(TRequest)] = request => handler.Handle((TRequest)request);
        }

        public bool Send<TRequest>(TRequest request) where TRequest : struct, IRequest
        {
            if (!handlers.TryGetValue(typeof(TRequest), out var handler))
            {
                return false;
            }

            handler(request);
            return true;
        }

        public int Count => handlers.Count;
    }
}
