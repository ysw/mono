//
// Binder.cs
//
// Authors:
//	Marek Safar  <marek.safar@gmail.com>
//
// Copyright (C) 2013 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Microsoft.CSharp.RuntimeBinder;

namespace PlayScript.Runtime
{
	public static class Binder
	{
		static readonly ConditionalWeakTable<object, Dictionary<string, object>> dynamic_classes = new ConditionalWeakTable<object, Dictionary<string, object>> ();
		
		public static dynamic GetMember (object instance, Type context, object name)
		{
			if (instance == null)
				throw GetNullObjectReferenceException ();
			
			var sname = GetName (name);

			// TODO: thread safety
			Dictionary<string, object> members;
			if (dynamic_classes.TryGetValue (instance, out members)) {
				object value;
				if (members.TryGetValue (sname, out value))
					return value;
			}

			var binder = Microsoft.CSharp.RuntimeBinder.Binder.GetMember (CSharpBinderFlags.None, sname, context, new[] { CSharpArgumentInfo.Create (CSharpArgumentInfoFlags.None, null) });
			var callsite = CallSite<Func<CallSite, object, object>>.Create (binder);

			// TODO: Add caching to avoid expensive Resolve
    		return callsite.Target (callsite, instance);
		}
		
		public static void SetMember (object instance, Type context, object name, object value)
		{
			if (instance == null)
				throw GetNullObjectReferenceException ();

			var sname = GetName (name);

			var binder = Microsoft.CSharp.RuntimeBinder.Binder.SetMember (CSharpBinderFlags.None, sname, context,
				new[] { CSharpArgumentInfo.Create (CSharpArgumentInfoFlags.None, null), CSharpArgumentInfo.Create (CSharpArgumentInfoFlags.None, null) });
			var callsite = CallSite<Func<CallSite, object, object, object>>.Create (binder);

			// TODO: Better handling
			try {
				// TODO: Add caching to avoid expensive Resolve
    			callsite.Target (callsite, instance, value);
    		} catch (RuntimeBinderException) { 	
				var members = dynamic_classes.GetOrCreateValue (instance);
							
				// TODO: Not thread safe
				members [sname] = value;
			}
		}

		public static bool HasProperty (object instance, Type context, object property)
		{
			if (instance == null)
				throw GetNullObjectReferenceException ();

			var type = instance as Type;
			var sname = GetName (property);
			bool static_only;

			if (type == null) {
				//
				// It's null when it's not static
				//
				// TODO: thread safety
				Dictionary<string, object> members;
				if (dynamic_classes.TryGetValue (instance, out members)) {
					if (members.ContainsKey (sname))
						return true;

				}

				type = instance.GetType ();
				static_only = false;
			} else {
				static_only = true;
			}

			var binder = new IsPropertyBinder (sname, context, static_only);	
			
			var callsite = CallSite<Func<CallSite, Type, bool>>.Create (binder);

			// TODO: Better handling
			try {
				// TODO: Add caching to avoid expensive Resolve
    			return callsite.Target (callsite, type);
    		} catch (RuntimeBinderException) {
    			throw;
			}
		}

		static string GetName (object name)
		{
			// TODO: Will be special token for null key enough?
			if (name == null)
				throw new NotImplementedException ("null name");
			
			return name.ToString ();
		}

		static Exception GetNullObjectReferenceException ()
		{
			return new _root.Error ("Cannot access a property or method of a null object reference.", 1009);			
		}
	}
}
